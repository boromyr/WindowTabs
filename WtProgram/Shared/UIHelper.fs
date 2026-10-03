namespace Bemo
open System
open System.Drawing
open System.Drawing.Text
open System.Windows.Forms
open Aga.Controls.Tree
open Bemo.Win32
open Bemo.Win32.Forms

[<AllowNullLiteral>]
type INode =
    abstract member showSettings : bool

// TreeViewAdv's own expand glyph is a fixed 9px image, tiny on scaled displays; this draws it to scale.
type ScaledPlusMinus() =
    inherit NodeControls.NodeControl()

    static member attach (tree:TreeViewAdv) (column:TreeColumn) =
        tree.ShowPlusMinus <- false
        tree.Indent <- Dpi.px 19
        let control = ScaledPlusMinus()
        control.ParentColumn <- column
        tree.NodeControls.Insert(0, control)

    member private this.width = Dpi.px 16

    override this.MeasureSize(node, context) = Size(this.width, this.width)

    override this.Draw(node, context) =
        if node.CanExpand then
            let r = context.Bounds
            let side = Dpi.px 9 ||| 1 // odd, so the sign sits in the middle
            let x = r.X + (this.width - side) / 2
            let y = r.Y + (r.Height - side) / 2
            use pen = new Pen(SystemColors.GrayText, float32(Dpi.px 1))
            let g = context.Graphics
            g.DrawRectangle(pen, x, y, side - 1, side - 1)
            let mid = side / 2
            let inset = max 2 (side / 4)
            g.DrawLine(pen, x + inset, y + mid, x + side - 1 - inset, y + mid)
            if node.IsExpanded.not then
                g.DrawLine(pen, x + mid, y + inset, x + mid, y + side - 1 - inset)

    override this.MouseDown(args) =
        if args.Button = MouseButtons.Left then
            args.Handled <- true
            if args.Node.CanExpand then
                args.Node.IsExpanded <- args.Node.IsExpanded.not

    // don't let a double click on the glyph also toggle the node
    override this.MouseDoubleClick(args) = args.Handled <- true

module internal UIText =
    let private resources = new System.Resources.ResourceManager("Properties.Resources", System.Reflection.Assembly.GetExecutingAssembly())
    let get (key:string) =
        match resources.GetString(key) with
        | null -> key
        | value -> value

type IntEditor() =
    let control =
        let control = FluentNumberBox()
        control.Minimum <- 1
        control.Maximum <- 1000
        control.Font <- FluentTheme.Body
        control.Size <- Size(Dpi.px 120, Dpi.px 32)
        control.Margin <- Padding(0)
        control
    interface IPropEditor with
        member x.value
            with get() = box(control.Value)
            and set(newValue) = control.Value <- unbox<int>(newValue)
        member x.control = control :> Control
        member x.changed = control.ValueChanged |> Event.map ignore

type TextEditor() =
    let control =
        let control = FluentTextBox()
        control.Font <- FluentTheme.Body
        control.Size <- Size(Dpi.px 240, Dpi.px 32)
        control
    interface IPropEditor with
        member x.value
            with get() = box(control.Text)
            and set(newValue) = control.Text <- unbox<string>(newValue)
        member x.control = control :> Control
        member x.changed = control.TextChanged |> Event.map ignore

type BoolEditor() =
    let control =
        let toggle = FluentToggle()
        toggle.Font <- FluentTheme.Body
        toggle.OnText <- UIText.get "On"
        toggle.OffText <- UIText.get "Off"
        toggle.Size <- toggle.GetPreferredSize(Size.Empty)
        toggle
    interface IPropEditor with
        member x.value
            with get() = box(control.Checked)
            and set(newValue) = control.Checked <- unbox<bool>(newValue)
        member x.control = control :> Control
        member x.changed = control.CheckedChanged |> Event.map ignore

type EnumEditor<'e when 'e :> Enum>() as this =
    let control =
        let combo = FluentComboBox()
        combo.Font <- FluentTheme.Body
        combo.Width <- Dpi.px 200
        combo
    let mutable cachedValue = null
    do this.init()

    member this.init() =
        for tag in Enum.GetValues(typeof<'e>) do
            let tag = tag.cast<'e>()
            control.Items.Add(tag.ToString()).ignore
        control.SelectedValueChanged.Add <| fun _ ->
            cachedValue <- control.SelectedItem

    member this.value
        with get() = 
            let value = cachedValue
            Enum.Parse(typeof<'e>, string(value)).cast<'e>()
        and set(value) =
            control.SelectedItem <- value.ToString()

    interface IPropEditor with
        member x.value
            with get() = this.value.cast<obj>()
            and set(value) = this.value <- value.cast<'e>()
        member x.control = control :> Control
        member x.changed = control.SelectedValueChanged |> Event.map ignore

type ColorEditor() as this =
    let changedEvent = Event<_>()
    // hex text box with the colour sample (which opens the colour picker) inside it
    let fluentTextBox =
        let box = FluentColorBox()
        box.Font <- FluentTheme.Body
        box.Size <- Size(Dpi.px 140, Dpi.px 32)
        box.Margin <- Padding(0)
        box.ColorChanged.Add <| fun _ ->
            (this :> IPropEditor).value <- box.Color
            changedEvent.Trigger()
        box

    let textBox =
        let tb = fluentTextBox.Inner
        let maxLen = 6
        let save() =
            (this :> IPropEditor).value <- this.colorFromTb
            changedEvent.Trigger()
        tb.CharacterCasing <- CharacterCasing.Upper
        tb.KeyPress.Add <| fun e ->
            try
                if e.KeyChar = (char)Keys.Enter then
                    e.Handled <- true
                    save()
                elif Char.IsControl(e.KeyChar).not then
                    if tb.Text.Length + 1 - tb.SelectionLength  > maxLen then raise (Exception())
                    Int32.Parse(e.KeyChar.ToString(), Globalization.NumberStyles.HexNumber).ignore
            with ex -> 
                e.Handled <- true            
        tb.Validating.Add <| fun e ->
            try
                if tb.Text.Length > maxLen then raise (Exception())
                this.colorFromTb.ignore
            with ex -> 
                e.Cancel <- true
                tb.SelectAll()
                MessageBox.Show(UIText.get "InvalidColor").ignore

        tb.Validated.Add <| fun e -> save()
        tb


    member this.colorFromTb =
        let text = textBox.Text
        let value = Int32.Parse(text, Globalization.NumberStyles.HexNumber)
        Color.FromRGB(value)

    member this.color = fluentTextBox.Color
    interface IPropEditor with
        member x.value
            with get() =
                let text = textBox.Text
                let value = Int32.Parse(text, Globalization.NumberStyles.HexNumber)
                box(Color.FromRGB(value))
            and set(newColor) =
                let color = unbox<Color>(newColor)
                fluentTextBox.Color <- color
                textBox.Text <- sprintf "%06X" (color.ToRGB())
        member x.control = fluentTextBox :> Control
        member x.changed = changedEvent.Publish


type HotKeyEditor() =
    let control =
        let box = FluentHotKeyBox()
        box.NoneText <- UIText.get "HotKeyNone"
        box.Font <- FluentTheme.Body
        box.Size <- Size(Dpi.px 200, Dpi.px 32)
        box
    interface IPropEditor with
        member x.value 
            with get() = box(control.HotKey)
            and set(newValue) = control.HotKey <- unbox<int>(newValue)
        member x.control = control :> Control
        member x.changed = control.HotKeyChanged |> Event.map (fun _ -> ())

type HotKeyModifiersEditor() as this =
    let mutable _modifiers = Keys.None
    let modifiersChanged = Event<_>()
    let textBox = {  
        new TextBox() with
            override x.ProcessCmdKey(msg, keys) =
                this.modifiers <- Keys.Modifiers &&& keys
                true
    }
    do
        this.modifiers <- Keys.None
    
    member this.modifiers 
        with get() = _modifiers
        and set(newValue) = 
            textBox.Text <- newValue.ToString()
            _modifiers <- newValue
            modifiersChanged.Trigger()

    interface IPropEditor with    
        member this.value 
            with get() = box(this.modifiers)
            and set(value) = this.modifiers <- unbox<Keys>(value)
        member this.control = textBox :> Control
        member this.changed = modifiersChanged.Publish

type HotKeyOnlyEditor() as this =
    let mutable _hk = Keys.None
    let hkChanged = Event<_>()
    let textBox = {  
        new TextBox() with
            override x.ProcessCmdKey(msg, keys) =
                this.hk <- Keys.KeyCode &&& keys
                true
    }
    do
        this.hk <- Keys.None
    
    member this.hk 
        with get() = _hk
        and set(newValue) = 
            textBox.Text <- newValue.ToString()
            _hk <- newValue
            hkChanged.Trigger()

    interface IPropEditor with    
        member this.value 
            with get() = box(this.hk)
            and set(value) = this.hk <- unbox<Keys>(value)
        member this.control = textBox :> Control
        member this.changed = hkChanged.Publish

type SmoothNodeTextBox() = 
    inherit NodeControls.NodeTextBox()

    override this.Draw(node, context) =
        context.Graphics.TextRenderingHint <- TextRenderingHint.ClearTypeGridFit
        base.Draw(node, context)


module UIHelper =
    open System.Resources
    open System.Reflection

    let label text =
        let label = Label()
        label.AutoSize <- true
        label.Text <- text
        label.TextAlign <- ContentAlignment.MiddleLeft
        label
        
    let resources = new ResourceManager("Properties.Resources", Assembly.GetExecutingAssembly());
    

    let form (fields:List2<_>) =
        let panel = 
            let t = TableLayoutPanel()
            t.AutoScroll <- true
            t.AutoSize <- true
            t.Dock <- DockStyle.Fill
            //t.Padding <- Padding(10)
            t.RowCount <- fields.length
            t.ColumnCount <- 2
            // Make control align right
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10f)) |> ignore
            t

        fields.enumerate.iter <| fun (i,(text, control:Control)) ->
            let caption = UIText.get text
            let label = label caption
            label.ForeColor <- FluentTheme.Text
            label.Anchor <- AnchorStyles.Left
            control.Dock <- DockStyle.Fill
            control.Margin <- Padding(Dpi.px 12, Dpi.px 4, 0, Dpi.px 4)
            label.Margin <- Padding(0,5,0,5)
            panel.Controls.Add(label)
            panel.Controls.Add(control)
            panel.SetRow(label, i)
            panel.SetColumn(label, 0)
            panel.SetRow(control, i)
            panel.SetColumn(control, 1)
        panel
              
    let vbox (controls:List2<Control>) =
        let t = 
            let t = TableLayoutPanel()
            t.AutoScroll <- true
            t.AutoSize <- true
            t.RowCount <- controls.length
            t.ColumnCount <- 1
            t
        controls.enumerate.iter <| fun(i,control) ->
            t.Controls.Add(control)
            t.SetRow(control, i)
            t.SetColumn(control, 0)
            t.RowStyles.Add(RowStyle()).ignore
        t  

    let hbox (controls:List2<Control>) =
        let t = 
            let t = TableLayoutPanel()
            t.AutoScroll <- true
            t.AutoSize <- true
            t.RowCount <- 1
            t.ColumnCount <- controls.length
            t
        controls.enumerate.iter <| fun(i,control) ->
            t.Controls.Add(control)
            t.SetRow(control, 0)
            t.SetColumn(control, i)
            t.ColumnStyles.Add(ColumnStyle()).ignore
        t  

    let okCancelForm control =
        let form = new FluentForm()
        form.Padding <- Padding(Dpi.px 16)
        form.FormBorderStyle <- FormBorderStyle.FixedDialog
        form.MinimizeBox <- false
        form.MaximizeBox <- false

        let okButton = FluentButton()
        okButton.Text <- "OK"
        okButton.Accent <- true
        okButton.Margin <- Padding(0, 0, Dpi.px 8, 0)
        okButton.Click.Add <| fun _ ->
            form.DialogResult <- DialogResult.OK

        let cancelButton = FluentButton()
        cancelButton.Text <- UIText.get "Cancel"
        cancelButton.Margin <- Padding(0)
        
        cancelButton.Click.Add <| fun _ ->
            form.DialogResult <- DialogResult.Cancel

        let buttonPanel = hbox (List2([okButton.cast<Control>(); cancelButton.cast<Control>()]))
        let vboxLayout = vbox (List2([control; buttonPanel.cast<Control>()]))
        vboxLayout.RowStyles.Item(0).SizeType <- SizeType.AutoSize
        vboxLayout.RowStyles.Item(1).SizeType <- SizeType.AutoSize
        buttonPanel.Anchor <- AnchorStyles.Bottom ||| AnchorStyles.Right
        buttonPanel.Margin <- Padding(0, Dpi.px 16, 0, 0)
        vboxLayout.Dock <- DockStyle.Fill
        form.Controls.Add(vboxLayout)
        form.AcceptButton <- okButton
        form.CancelButton <- cancelButton
        form
