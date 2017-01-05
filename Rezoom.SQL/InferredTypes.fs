﻿module Rezoom.SQL.InferredTypes
open Rezoom.SQL
open System
open System.Collections.Generic

type TypeVariableId = int

type CoreInferredType =
    | TypeKnown of CoreColumnType
    | TypeVariable of TypeVariableId

type InferredNullable =
    | NullableUnknown
    | NullableKnown of bool
    | NullableVariable of TypeVariableId
    | NullableEither of InferredNullable * InferredNullable
    static member Either(left, right) =
        match left, right with
        | (NullableUnknown | NullableKnown false), x
        | x, (NullableUnknown | NullableKnown false) -> x
        | NullableKnown true as t, _ -> t
        | _, (NullableKnown true as t) -> t
        | NullableVariable x as v, NullableVariable y -> v
        | l, r -> NullableEither(l, r)
    member this.Simplify() =
        match this with
        | NullableUnknown -> NullableKnown false
        | NullableKnown false
        | NullableKnown true
        | NullableVariable _ -> this
        | NullableEither (l, r) ->
            match l.Simplify(), r.Simplify() with
            | NullableKnown true, _
            | _, NullableKnown true -> NullableKnown true
            | NullableKnown false, x -> x
            | x, NullableKnown false -> x
            | l, r -> NullableEither(l, r)

type InferredType =
    {   InferredType : CoreInferredType
        InferredNullable : InferredNullable
    }
    static member Of(col) = { InferredNullable = NullableKnown col.Nullable; InferredType = TypeKnown col.Type }
    static member Of(core) = { InferredNullable = NullableUnknown; InferredType = TypeKnown core }
    static member Float = InferredType.Of(FractionalTypeClass)
    static member Integer = InferredType.Of(IntegralTypeClass)
    static member Number = InferredType.Of(NumericTypeClass)
    static member String = InferredType.Of(StringType)
    static member Boolean = InferredType.Of(BooleanType)
    static member DateTime = InferredType.Of(DateTimeType)
    static member DateTimeOffset = InferredType.Of(DateTimeOffsetType)
    static member Blob = InferredType.Of(BinaryType)
    static member Any = InferredType.Of(AnyTypeClass)
    static member Dependent(ifNull : InferredType, outputType : CoreColumnType) =
        {   InferredNullable = ifNull.InferredNullable
            InferredType = TypeKnown outputType
        }
    static member OfLiteral(literal : Literal) =
        match literal with
        | NullLiteral -> { InferredNullable = NullableKnown true; InferredType = TypeKnown AnyTypeClass }
        | BooleanLiteral _ -> InferredType.Boolean
        | StringLiteral _ -> InferredType.String
        | BlobLiteral _ -> InferredType.Blob
        | NumericLiteral (IntegerLiteral _) -> InferredType.Number
        | NumericLiteral (FloatLiteral _) -> InferredType.Float
        | DateTimeLiteral _ -> InferredType.DateTime
        | DateTimeOffsetLiteral _ -> InferredType.DateTimeOffset
    static member OfTypeName(typeName : TypeName, inputType : InferredType) =
        let affinity = CoreColumnType.OfTypeName(typeName)
        {   InferredNullable = inputType.InferredNullable
            InferredType = TypeKnown affinity
        }

type InfExprType = ExprType<InferredType ObjectInfo, InferredType ExprInfo>
type InfExpr = Expr<InferredType ObjectInfo, InferredType ExprInfo>
type InfInExpr = InExpr<InferredType ObjectInfo, InferredType ExprInfo>
type InfCollationExpr = CollationExpr<InferredType ObjectInfo, InferredType ExprInfo>
type InfBetweenExpr = BetweenExpr<InferredType ObjectInfo, InferredType ExprInfo>
type InfSimilarityExpr = SimilarityExpr<InferredType ObjectInfo, InferredType ExprInfo>
type InfBinaryExpr = BinaryExpr<InferredType ObjectInfo, InferredType ExprInfo>
type InfUnaryExpr = UnaryExpr<InferredType ObjectInfo, InferredType ExprInfo>
type InfObjectName = ObjectName<InferredType ObjectInfo>
type InfColumnName = ColumnName<InferredType ObjectInfo>
type InfInSet = InSet<InferredType ObjectInfo, InferredType ExprInfo>
type InfCaseExpr = CaseExpr<InferredType ObjectInfo, InferredType ExprInfo>
type InfCastExpr = CastExpr<InferredType ObjectInfo, InferredType ExprInfo>
type InfFunctionInvocationExpr = FunctionInvocationExpr<InferredType ObjectInfo, InferredType ExprInfo>
    
type InfWithClause = WithClause<InferredType ObjectInfo, InferredType ExprInfo>
type InfCommonTableExpression = CommonTableExpression<InferredType ObjectInfo, InferredType ExprInfo>
type InfCompoundExprCore = CompoundExprCore<InferredType ObjectInfo, InferredType ExprInfo>
type InfCompoundExpr = CompoundExpr<InferredType ObjectInfo, InferredType ExprInfo>
type InfCompoundTermCore = CompoundTermCore<InferredType ObjectInfo, InferredType ExprInfo>
type InfCompoundTerm = CompoundTerm<InferredType ObjectInfo, InferredType ExprInfo>
type InfCreateTableDefinition = CreateTableDefinition<InferredType ObjectInfo, InferredType ExprInfo>
type InfCreateTableStmt = CreateTableStmt<InferredType ObjectInfo, InferredType ExprInfo>
type InfSelectCore = SelectCore<InferredType ObjectInfo, InferredType ExprInfo>
type InfJoinConstraint = JoinConstraint<InferredType ObjectInfo, InferredType ExprInfo>
type InfJoin = Join<InferredType ObjectInfo, InferredType ExprInfo>
type InfLimit = Limit<InferredType ObjectInfo, InferredType ExprInfo>
type InfGroupBy = GroupBy<InferredType ObjectInfo, InferredType ExprInfo>
type InfOrderingTerm = OrderingTerm<InferredType ObjectInfo, InferredType ExprInfo>
type InfResultColumn = ResultColumn<InferredType ObjectInfo, InferredType ExprInfo>
type InfResultColumns = ResultColumns<InferredType ObjectInfo, InferredType ExprInfo>
type InfTableOrSubquery = TableOrSubquery<InferredType ObjectInfo, InferredType ExprInfo>
type InfTableExprCore = TableExprCore<InferredType ObjectInfo, InferredType ExprInfo>
type InfTableExpr = TableExpr<InferredType ObjectInfo, InferredType ExprInfo>
type InfTableInvocation = TableInvocation<InferredType ObjectInfo, InferredType ExprInfo>
type InfSelectStmt = SelectStmt<InferredType ObjectInfo, InferredType ExprInfo>
type InfColumnConstraint = ColumnConstraint<InferredType ObjectInfo, InferredType ExprInfo>
type InfColumnDef = ColumnDef<InferredType ObjectInfo, InferredType ExprInfo>
type InfAlterTableStmt = AlterTableStmt<InferredType ObjectInfo, InferredType ExprInfo>
type InfAlterTableAlteration = AlterTableAlteration<InferredType ObjectInfo, InferredType ExprInfo>
type InfCreateIndexStmt = CreateIndexStmt<InferredType ObjectInfo, InferredType ExprInfo>
type InfTableIndexConstraintClause = TableIndexConstraintClause<InferredType ObjectInfo, InferredType ExprInfo>
type InfTableConstraint = TableConstraint<InferredType ObjectInfo, InferredType ExprInfo>
type InfCreateViewStmt = CreateViewStmt<InferredType ObjectInfo, InferredType ExprInfo>
type InfQualifiedTableName = QualifiedTableName<InferredType ObjectInfo>
type InfDeleteStmt = DeleteStmt<InferredType ObjectInfo, InferredType ExprInfo>
type InfDropObjectStmt = DropObjectStmt<InferredType ObjectInfo>
type InfUpdateStmt = UpdateStmt<InferredType ObjectInfo, InferredType ExprInfo>
type InfInsertStmt = InsertStmt<InferredType ObjectInfo, InferredType ExprInfo>
type InfStmt = Stmt<InferredType ObjectInfo, InferredType ExprInfo>
type InfVendorStmt = VendorStmt<InferredType ObjectInfo, InferredType ExprInfo>
type InfTotalStmt = TotalStmt<InferredType ObjectInfo, InferredType ExprInfo>

type ITypeInferenceContext =
    abstract member AnonymousVariable : unit -> InferredType
    abstract member Variable : BindParameter -> InferredType
    /// Unify the two types (ensure they are compatible and add constraints)
    /// and produce the most specific type.
    abstract member Unify : SourceInfo * InferredType * InferredType -> InferredType
    abstract member ForceNullable : SourceInfo * InferredNullable -> unit
    abstract member Concrete : InferredType -> ColumnType
    abstract member Parameters : BindParameter seq

type InferredQueryColumn() =
    static member OfColumn(fromAlias : Name option, column : SchemaColumn) =
        {   Expr =
                {   Source = SourceInfo.Invalid
                    Info = { ExprInfo<_>.OfType(InferredType.Of(column.ColumnType)) with Column = Some column }
                    Value = ColumnNameExpr { ColumnName = column.ColumnName; Table = None }
                }
            ColumnName = column.ColumnName
            FromAlias = fromAlias
        }

let foundAt source nameResolution =
    match nameResolution with
    | Found x -> x
    | NotFound err
    | Ambiguous err -> failAt source err

let inferredOfTable (table : SchemaTable) =
    {   Columns =
            table.Columns
            |> Seq.map (function KeyValue(_, c) -> InferredQueryColumn.OfColumn(Some table.TableName, c))
            |> toReadOnlyList
    }

let inferredOfView (view : SchemaView) =
    let concreteQuery = view.Definition.Value.Info.Query
    concreteQuery.Map(InferredType.Of)

type InferredFromClause =
    {   /// The tables named in the "from" clause of the query, if any.
        /// These are keyed on the alias of the table, if any, or the table name.
        FromVariables : IReadOnlyDictionary<Name, InferredType ObjectInfo>
        /// All the objects involved in the from clause in order.
        FromObjects : (Name * InferredType ObjectInfo) seq
    }
    static member FromSingleObject(tableName : InfObjectName) =
        {   FromVariables = Dictionary() :> IReadOnlyDictionary<_, _>
            FromObjects = (Name(""), tableName.Info) |> Seq.singleton
        }
    member this.ResolveTable(tableName : ObjectName) =
        match tableName.SchemaName with
        // We don't currently support referencing columns like "main.users.id". Use table aliases instead!
        | Some schemaName -> Ambiguous <| sprintf "Unsupported schema name in column reference: ``%O``" tableName
        | None ->
            let succ, query = this.FromVariables.TryGetValue(tableName.ObjectName)
            if succ then Found query
            else NotFound <| sprintf "No such table in FROM clause: ``%O``" tableName.ObjectName
    member this.ResolveColumnReference(name : ColumnName) =
        match name.Table with
        | None ->
            let matches =
                seq {
                    for tableAlias, objectInfo in this.FromObjects do
                        let table = objectInfo.Table
                        match table.Query.ColumnByName(name.ColumnName) with
                        | Found column ->
                            yield Ok ((if tableAlias.Value = "" then None else Some tableAlias), table, column)
                        | NotFound _ -> ()
                        | Ambiguous err -> yield Error err
                } |> toReadOnlyList
            if matches.Count = 1 then
                match matches.[0] with
                | Ok triple -> Found triple
                | Error e -> Ambiguous e
            elif matches.Count <= 0 then
                NotFound <| sprintf "No such column in FROM clause: ``%O``" name
            else
                Ambiguous <| sprintf "Ambiguous column: ``%O``" name
        | Some tableName ->
            match this.ResolveTable(tableName) with
            | Found objectInfo ->
                let table = objectInfo.Table
                match table.Query.ColumnByName(name.ColumnName) with
                | Found column -> Found (Some tableName.ObjectName, table, column)
                | NotFound err -> NotFound err
                | Ambiguous err -> Ambiguous err
            | NotFound err -> NotFound err
            | Ambiguous err -> Ambiguous err

and InferredSelectScope =
    {   /// If this scope is that of a subquery, the parent query's scope can also be used
        /// to resolve column and CTE names.
        ParentScope : InferredSelectScope option
        /// The model this select is running against.
        /// This includes tables and views that are part of the database, and may be used to resolve
        /// table names in the "from" clause of the query.
        Model : Model
        /// Any CTEs defined by the query.
        /// These may be referenced in the "from" clause of the query.
        CTEVariables : Map<Name, InferredType QueryExprInfo>
        FromClause : InferredFromClause option
        SelectClause : InferredType QueryExprInfo option
    }

    static member Root(model) =
        {   ParentScope = None
            Model = model
            CTEVariables = Map.empty
            FromClause = None
            SelectClause = None
        }

    member private this.ResolveObjectReferenceBySchema(schema : Schema, name : Name) =
        match schema.Objects |> Map.tryFind name with
        | Some (SchemaTable tbl) ->
            { Table = TableReference tbl; Query = inferredOfTable(tbl) } |> TableLike |> Found
        | Some (SchemaView view) ->
            { Table = ViewReference view; Query = inferredOfView(view) } |> TableLike |> Found
        | None -> NotFound <| sprintf "No such table in schema %O: ``%O``" schema.SchemaName name

    /// Resolve a reference to a table which may occur as part of a TableExpr.
    /// This will resolve against the database model and CTEs, but not table aliases defined in the FROM clause.
    member this.ResolveObjectReference(name : ObjectName) =
        match name.SchemaName with
        | None ->
            match this.CTEVariables.TryFind(name.ObjectName) with
            | Some cte -> { Table = CTEReference name.ObjectName; Query = cte } |> TableLike |> Found
            | None ->
                match this.ParentScope with
                | Some parent ->
                    parent.ResolveObjectReference(name)
                | None ->
                    let schema = this.Model.Schemas.[this.Model.DefaultSchema]
                    this.ResolveObjectReferenceBySchema(schema, name.ObjectName)
        | Some schema ->
            let schema = this.Model.Schemas.[schema]
            this.ResolveObjectReferenceBySchema(schema, name.ObjectName)

    /// Resolve a column reference, which may be qualified with a table alias.
    /// This resolves against the tables referenced in the FROM clause, and the columns explicitly named
    /// in the SELECT clause, if any.
    member this.ResolveColumnReference(name : ColumnName) =
        let findFrom() =
            let thisLevel =
                match this.FromClause with
                | None ->
                    NotFound <| sprintf "Cannot reference column name ``%O`` in query without a FROM clause" name
                | Some fromClause ->
                    fromClause.ResolveColumnReference(name)
            match this.ParentScope, thisLevel with
            | Some parent, NotFound _ ->
                parent.ResolveColumnReference(name)
            | _ -> thisLevel
        match name.Table, this.SelectClause with
        | None, Some selected ->
            match selected.ColumnByName(name.ColumnName) with
            | Found column ->
                Found (None, { Table = SelectClauseReference name.ColumnName; Query = selected }, column)
            | Ambiguous reason -> Ambiguous reason
            | NotFound _ -> findFrom()
        | _ -> findFrom()

let concreteMapping (inference : ITypeInferenceContext) =
    ASTMapping<InferredType ObjectInfo, InferredType ExprInfo, _, _>
        ((fun t -> t.Map(inference.Concrete)), fun e -> e.Map(inference.Concrete))