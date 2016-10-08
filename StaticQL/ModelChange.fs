﻿namespace StaticQL
open System
open System.Collections.Generic
open StaticQL.InferredTypes

type private ModelChange(model : Model, inference : ITypeInferenceContext) =
    member private this.CreateTableColumns(schemaName : Name, tableName : Name, def : InfCreateTableDefinition) =
        let tablePkColumns =
            seq {
                for constr in def.Constraints do
                    match constr.TableConstraintType with
                    | TableIndexConstraint { Type = PrimaryKey; IndexedColumns = indexed } ->
                        for expr, _ in indexed do
                            match expr.Value with
                            | ColumnNameExpr name -> yield name.ColumnName
                            | _ -> ()
                    | _ -> ()
            } |> Set.ofSeq
        [| for column in def.Columns ->
            let affinity = column.Type |> Option.map InferredType.Affinity |? AnyType
            let hasNotNullConstraint =
                column.Constraints
                |> Seq.exists(function | { ColumnConstraintType = NotNullConstraint _ } -> true | _ -> false)
            let isPrimaryKey =
                tablePkColumns.Contains(column.Name)
                || column.Constraints |> Seq.exists(function
                    | { ColumnConstraintType = PrimaryKeyConstraint _ } -> true
                    | _ -> false)
            {   SchemaName = schemaName
                TableName = tableName
                PrimaryKey = isPrimaryKey
                ColumnName = column.Name
                ColumnType = { Type = affinity; Nullable = not hasNotNullConstraint }
            }
        |]
    member private this.CreateTableColumns(model, schemaName : Name, tableName : Name, asSelect : InfSelectStmt) =
        let query = asSelect.Value.Info.Query
        [| for column in query.Columns ->
            {   SchemaName = schemaName
                TableName = tableName
                ColumnName = column.ColumnName
                PrimaryKey = column.Expr.Info.PrimaryKey
                ColumnType = inference.Concrete(column.Expr.Info.Type) // unfortunate but necessary
            }
        |]
    member private this.CreateTable(create : InfCreateTableStmt) =
        let defaultSchema = if create.Temporary then model.TemporarySchema else model.DefaultSchema
        let schema = create.Name.SchemaName |? defaultSchema
        match model.Schemas.TryFind(schema) with
        | None -> failAt create.Name.Source <| sprintf "No such schema: ``%O``" schema
        | Some schema ->
            let tableName = create.Name.ObjectName
            if schema.Tables.ContainsKey(tableName) then
                if create.IfNotExists then None
                else failAt create.Name.Source <| sprintf "Table ``%O`` already exists" create.Name
            else
                let columns =
                    match create.As with
                    | CreateAsSelect select -> this.CreateTableColumns(model, schema.SchemaName, tableName, select)
                    | CreateAsDefinition def -> this.CreateTableColumns(schema.SchemaName, tableName, def)  
                let table =
                    {   SchemaName = schema.SchemaName
                        TableName = create.Name.ObjectName
                        Columns = columns |> Set.ofSeq
                    }
                let schema =
                    { schema with Tables = schema.Tables |> Map.add table.TableName table }
                Some { model with Schemas = model.Schemas |> Map.add schema.SchemaName schema }
    member this.AlterTable(alter : InfAlterTableStmt) =
        let schemaName = alter.Table.SchemaName |? model.DefaultSchema
        match model.Schemas |> Map.tryFind schemaName with
        | None -> failAt alter.Table.Source <| sprintf "No such schema: ``%O``" alter.Table
        | Some schema ->
            let tblName = alter.Table.ObjectName
            match schema.Tables |> Map.tryFind tblName with
            | None -> failAt alter.Table.Source <| sprintf "No such table: ``%O``" alter.Table
            | Some tbl ->
                match alter.Alteration with
                | RenameTo newName ->
                    match schema.Tables |> Map.tryFind newName with
                    | None ->
                        let tables =
                            schema.Tables |> Map.remove tblName |> Map.add newName tbl
                        let schema = { schema with Tables = tables }
                        Some { model with Schemas = model.Schemas |> Map.add schema.SchemaName schema }
                    | Some existing ->
                        failAt alter.Table.Source <| sprintf "Table ``%O`` already exists" existing
                | AddColumn col ->
                    match tbl.Columns |> Seq.tryFind (fun c -> c.ColumnName = col.Name) with
                    | Some _ -> failAt alter.Table.Source <| sprintf "Column ``%O`` already exists" col.Name
                    | None ->
                        let affinity = col.Type |> Option.map InferredType.Affinity |? AnyType
                        let hasNotNullConstraint =
                            col.Constraints
                            |> Seq.exists(
                                function | { ColumnConstraintType = NotNullConstraint _ } -> true | _ -> false)
                        let isPrimaryKey =
                            col.Constraints
                            |> Seq.exists(
                                function | { ColumnConstraintType = PrimaryKeyConstraint _ } -> true | _ -> false)
                        let newCol =
                            {   SchemaName = schemaName
                                TableName = tblName
                                PrimaryKey = isPrimaryKey
                                ColumnName = col.Name
                                ColumnType = { Type = affinity; Nullable = not hasNotNullConstraint }
                            }
                        let newTbl = { tbl with Columns = tbl.Columns |> Set.add newCol }
                        let schema = { schema with Tables = schema.Tables |> Map.add tbl.TableName newTbl }
                        Some { model with Schemas = model.Schemas |> Map.add schema.SchemaName schema }
    member this.CreateView(create : InfCreateViewStmt) =
        let schemaName = create.ViewName.SchemaName |? model.DefaultSchema
        match model.Schemas |> Map.tryFind schemaName with
        | None -> failAt create.ViewName.Source <| sprintf "No such schema: ``%O``" create.ViewName
        | Some schema ->
            if schema.ContainsObject(create.ViewName.ObjectName) then
                failAt create.ViewName.Source <| sprintf "Object already exists: ``%O``" create.ViewName
            else
                let view =
                    {   SchemaName = schema.SchemaName
                        ViewName = create.ViewName.ObjectName
                        Columns =
                            seq {
                                for column in create.AsSelect.Value.Info.Columns ->
                                    {   SchemaName = schemaName
                                        TableName = create.ViewName.ObjectName
                                        ColumnName = column.ColumnName
                                        PrimaryKey = column.Expr.Info.PrimaryKey
                                        ColumnType = inference.Concrete(column.Expr.Info.Type)
                                    }
                            } |> Set.ofSeq
                        ReferencedTables = Set.empty // TODO
                    }
                let schema = { schema with Views = schema.Views |> Map.add create.ViewName.ObjectName view }
                Some { model with Schemas = model.Schemas |> Map.add schema.SchemaName schema }
    member this.DropObject(drop : InfDropObjectStmt) =
        let schemaName = drop.ObjectName.SchemaName |? model.DefaultSchema
        let objName = drop.ObjectName.ObjectName
        match model.Schemas |> Map.tryFind schemaName with
        | None -> failAt drop.ObjectName.Source <| sprintf "No such schema: ``%O``" objName
        | Some schema ->
            let update name map newSchema =
                match map |> Map.tryFind objName with
                | None ->
                    if drop.IfExists then None
                    else failAt drop.ObjectName.Source <| sprintf "No such %s: ``%O``" name objName
                | Some _ ->
                    Some { model with Schemas = model.Schemas |> Map.add schema.SchemaName (newSchema()) }
            match drop.Drop with
            | DropTable ->
                update "table" schema.Tables <| fun () -> { schema with Tables = Map.remove objName schema.Tables }
            | DropView ->
                update "view" schema.Views <| fun () -> { schema with Views = Map.remove objName schema.Views }
            | DropIndex
            | DropTrigger -> None // TODO: track these in schema
            
    member this.Statment(stmt : InfStmt) =
        match stmt with
        | AlterTableStmt alter -> this.AlterTable(alter)
        | CreateTableStmt create -> this.CreateTable(create)
        | CreateViewStmt create -> this.CreateView(create)
        | CreateIndexStmt create -> failwith "not implemented"
        | CreateTriggerStmt create -> failwith "not implemented"
        | DropObjectStmt drop -> this.DropObject(drop)
        | BeginStmt
        | CommitStmt
        | DeleteStmt _
        | InsertStmt _
        | RollbackStmt
        | SelectStmt _
        | UpdateStmt _ -> None

