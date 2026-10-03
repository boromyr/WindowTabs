namespace Bemo
open System
open System.Collections.Generic
open System.Drawing
open System.IO
open System.Windows.Forms
open Bemo.Win32.Forms
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Aga.Controls
open Aga.Controls.Tree
open System.Resources
open System.Reflection

[<AllowNullLiteral>]
type WorkspaceNode(model:Dynamic) as this =
    inherit Node()
    do
        let children = model?children : List2<Dynamic>
        children.iter <| fun(child) -> 
            this.Nodes.Add(WorkspaceNode(child))
        model.cast<IWorkspaceNode>().removed.Add this.onRemoved

    member this.model = model

    member this.showSettings = this.model?showSettings
    member this.icon = this.model?icon
    member this.name = this.model?name
    member this.title = 
        if this.showSettings then this.model?title else null
    member this.matchType = 
        if this.showSettings then box(this.model?matchType.ToString()) else null

    member this.onRemoved() =
        this.Parent.Nodes.Remove(this).ignore


type WorkspaceView() as this =
    let Cell = CellScope()
    
    let resources = new ResourceManager("Properties.Resources", Assembly.GetExecutingAssembly());

    member this.wm = Cell.cacheProp this <| fun() ->
        let wm = WorkspaceModel()
        wm.workspaceAdded.Add this.onWorkspaceAdded
        wm.init()
        wm

    member this.nameColumn = Cell.cacheProp this <| fun() ->
        TreeColumn(resources.GetString("Name"), Dpi.px 200)

    member this.matchTypeColumn = Cell.cacheProp this <| fun() ->
        TreeColumn(resources.GetString("MatchType"), Dpi.px 170)

    member this.titleColumn = Cell.cacheProp this <| fun() ->
        TreeColumn(resources.GetString("Title"), Dpi.px 350)
        
    member this.model = Cell.cacheProp this <| fun() -> 
        let model = TreeModel()
        model

    member this.panel : Control = Cell.cacheProp this <| fun() ->
        FluentUI.fillPage (resources.GetString("Workspace")) [this.toolbar] this.tree

    member this.iconNodeControl = Cell.cacheProp this <| fun() ->
        let control = NodeControls.NodeStateIcon()
        control.ParentColumn <- this.nameColumn
        control.DataPropertyName <- "icon"
        control.LeftMargin <- Dpi.px 3
        control

    member this.textNodeControl = Cell.cacheProp this <| fun() ->
        let control = SmoothNodeTextBox()
        control.Trimming <- StringTrimming.EllipsisCharacter
        control.DisplayHiddenContentInToolTip <- true
        control.ParentColumn <- this.nameColumn
        control.DataPropertyName <- "name"
        control.LeftMargin <- Dpi.px 3
        control

    member this.titleNodeControl = Cell.cacheProp this <| fun() ->
        let control = SmoothNodeTextBox()
        control.Trimming <- StringTrimming.EllipsisCharacter
        control.DisplayHiddenContentInToolTip <- true
        control.ParentColumn <- this.titleColumn
        control.DataPropertyName <- "title"
        control.LeftMargin <- Dpi.px 3
        control

    member this.matchTypeNodeControl = Cell.cacheProp this <| fun() ->
        let control = SmoothNodeTextBox()
        control.Trimming <- StringTrimming.EllipsisCharacter
        control.DisplayHiddenContentInToolTip <- true
        control.ParentColumn <- this.matchTypeColumn
        control.DataPropertyName <- "matchType"
        control.LeftMargin <- Dpi.px 3
        control

    member this.tree = Cell.cacheProp this <| fun() ->
        let tree = TreeViewAdv()
        tree.FullRowSelect <- true
        tree.UseColumns <- true
        tree.RowHeight <- Dpi.px 24
        Dpi.scaleTreeViewHeader tree
        tree.Columns.Add(this.nameColumn)
        tree.Columns.Add(this.matchTypeColumn)
        tree.Columns.Add(this.titleColumn)
        tree.NodeControls.Add(this.iconNodeControl)
        tree.NodeControls.Add(this.textNodeControl)
        tree.NodeControls.Add(this.matchTypeNodeControl)
        tree.NodeControls.Add(this.titleNodeControl)
        ScaledPlusMinus.attach tree this.nameColumn
        tree.Model <- this.model
        tree.Dock <- DockStyle.Fill
        tree.SelectionChanged.Add <| this.onTreeSelectionChanged
        tree.Font <- FluentTheme.Body
        tree.BackColor <- FluentTheme.Card
        tree.ForeColor <- FluentTheme.Text
        tree.BorderStyle <- BorderStyle.None
        tree

    member this.newButton : FluentButton = Cell.cacheProp this <| fun() ->
        FluentUI.button (Some "") (resources.GetString("New")) (fun () -> this.onNewButton())

    member this.restoreButton : FluentButton = Cell.cacheProp this <| fun() ->
        let btn = FluentUI.button (Some "") (resources.GetString("Restore")) (fun () -> this.onRestoreButton())
        this.wm.canRestoreChanged.Add <| fun(canRestore) -> 
            btn.Enabled <- canRestore
        btn

    member this.removeButton : FluentButton = Cell.cacheProp this <| fun() ->
        FluentUI.button (Some "") (resources.GetString("Remove")) (fun () -> this.onRemoveButton())

    member this.editButton : FluentButton = Cell.cacheProp this <| fun() ->
        FluentUI.button (Some "") (resources.GetString("Edit")) (fun () -> this.onEditButton())

    member this.toolbar : Control = Cell.cacheProp this <| fun() ->
        FluentUI.row [this.newButton; this.restoreButton; this.editButton; this.removeButton]
    member this.findNode(node:TreeNodeAdv) =
        this.model.FindNode(this.tree.GetPath(node)) :?> WorkspaceNode

    member this.onTreeSelectionChanged(e) =
        let node = this.tree.SelectedNode
        let model =
            if node <> null then
                this.findNode(node).model
            else
                null
        this.wm.selected <- model

    member this.onNewButton() =
        this.wm.create()

    member this.onRestoreButton() =
        this.wm.restore()

    member this.onRemoveButton() =
        this.wm.remove()
        
    member this.onEditButton() =
        if this.wm.edit(this.panel) then
            this.tree.FullUpdate()

    member this.onWorkspaceAdded(ws:Workspace) =
        this.tree.Root.CollapseAll()
        let wsNode = WorkspaceNode(ws)
        this.model.Nodes.Insert(0, wsNode)
        let path = this.model.GetPath(wsNode)
        let treeNode = this.tree.FindNode(path)
        treeNode.ExpandAll()
        InvokerService.invoker.asyncInvoke <| fun() ->
            this.tree.SelectedNode <- treeNode

    interface ISettingsView with
        member x.key = SettingsViewType.LayoutSettings
        member x.title = resources.GetString("Workspace")
        member x.control = this.panel
