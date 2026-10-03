namespace Bemo
open System
open System.Drawing
open System.Reflection
open System.Resources
open System.Windows.Forms
open Bemo.Win32.Forms

/// Building blocks for settings pages in the Windows 11 Settings style.
/// Sizes are in 96-DPI pixels and scaled with Dpi.px, since the process is DPI aware.
module FluentUI =

    let private resources = new ResourceManager("Properties.Resources", Assembly.GetExecutingAssembly())

    /// Localized text for a resource key (the key itself if there is no translation).
    let text (key:string) =
        match resources.GetString(key) with
        | null -> key
        | value -> value

    let label (text:string) =
        let label = Label()
        label.AutoSize <- true
        label.Text <- text
        label.ForeColor <- FluentTheme.Text
        label.BackColor <- Color.Transparent
        label.Margin <- Padding(0)
        label

    let secondaryLabel (text:string) =
        let label = label text
        label.Font <- FluentTheme.Caption
        label.ForeColor <- FluentTheme.TextSecondary
        label

    let sectionHeader (text:string) =
        let label = label text
        label.Font <- FluentTheme.BodyStrong
        label.Margin <- Padding(Dpi.px 2, Dpi.px 20, 0, Dpi.px 8)
        label :> Control

    let private titleLabel (text:string) =
        let label = label text
        label.Font <- FluentTheme.Title
        label.Margin <- Padding(0, 0, 0, Dpi.px 8)
        label :> Control

    /// A row of the page: optional icon, title/description on the left, the control on the right.
    let settingCardWithIcon (icon:Image option) (title:string) (description:string option) (control:Control) =
        let card = new FluentSettingCard()
        card.Padding <- Padding(Dpi.px 16, 0, Dpi.px 16, 0)
        card.Margin <- Padding(0, 0, 0, Dpi.px 4)
        card.Height <- Dpi.px (if description.IsSome then 68 else 58)
        card.Title <- title
        description.iter <| fun text -> card.Description <- text
        icon.iter <| fun image -> card.Icon <- image
        card.Content <- control
        card :> Control

    let settingCard title description control = settingCardWithIcon None title description control

    /// A row with an on/off switch, painted by the card itself.
    let toggleCard (title:string) (isChecked:bool) (onChange: bool -> unit) =
        let card = new FluentToggleCard()
        card.Padding <- Padding(Dpi.px 16, 0, Dpi.px 16, 0)
        card.Margin <- Padding(0, 0, 0, Dpi.px 4)
        card.Height <- Dpi.px 58
        card.Title <- title
        card.OnText <- text "On"
        card.OffText <- text "Off"
        card.Checked <- isChecked
        card.CheckedChanged.Add <| fun _ -> onChange card.Checked
        card :> Control

    /// Controls side by side, e.g. a group of buttons.
    let row (controls:Control list) =
        let flow = FlowLayoutPanel()
        flow.AutoSize <- true
        flow.AutoSizeMode <- AutoSizeMode.GrowAndShrink
        flow.WrapContents <- false
        flow.BackColor <- Color.Transparent
        flow.Margin <- Padding(0)
        flow.Padding <- Padding(0)
        controls |> List.iteri (fun i control ->
            control.Margin <- Padding(0, 0, (if i = controls.Length - 1 then 0 else Dpi.px 8), 0)
            flow.Controls.Add(control))
        flow :> Control

    let button (glyph:string option) (text:string) (onClick: unit -> unit) =
        let button = FluentButton()
        button.Text <- text
        glyph.iter <| fun glyph -> button.Glyph <- glyph
        button.Click.Add <| fun _ -> onClick()
        button

    /// Vertical list of controls stretched to the available width.
    let stack (controls:Control list) =
        let stack = new FluentStackPanel()
        stack.Margin <- Padding(0)
        stack.Padding <- Padding(0)
        stack.SuspendLayout()
        controls |> List.iter (fun control -> stack.Controls.Add(control))
        stack.ResumeLayout()
        stack

    let private pagePadding() = Padding(Dpi.px 36, Dpi.px 24, Dpi.px 36, Dpi.px 24)

    /// Panel that draws without flicker while it scrolls or resizes.
    type private PagePanel() as this =
        inherit Panel()
        do this.SetStyle(ControlStyles.OptimizedDoubleBuffer ||| ControlStyles.AllPaintingInWmPaint, true)

    /// A scrolling page: title on top, then the given rows.
    let page (title:string) (rows:Control list) =
        let outer = new PagePanel()
        outer.Dock <- DockStyle.Fill
        outer.AutoScroll <- true
        outer.BackColor <- FluentTheme.Background
        outer.Padding <- pagePadding()
        let content = stack (titleLabel title :: rows)
        // Not docked: the width always leaves room for the scroll bar, so its appearing doesn't
        // resize (and move the window of) every row on the page.
        content.Location <- Point(outer.Padding.Left, outer.Padding.Top)
        let fitWidth() =
            content.Width <- max 0 (outer.Width - outer.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth)
        outer.Resize.Add <| fun _ -> fitWidth()
        fitWidth()
        outer.AutoScrollMargin <- Size(0, outer.Padding.Bottom)
        outer.Controls.Add(content)
        FluentTheme.UseDarkScrollBars(outer)
        outer :> Control

    /// A page whose body fills the remaining height (lists, logs): title, header rows, then the body in a card.
    let fillPage (title:string) (header:Control list) (body:Control) =
        let outer = new PagePanel()
        outer.Dock <- DockStyle.Fill
        outer.BackColor <- FluentTheme.Background
        outer.Padding <- pagePadding()

        let card = FluentCard()
        card.Dock <- DockStyle.Fill
        card.Padding <- Padding(Dpi.px 8)
        body.Dock <- DockStyle.Fill
        card.Controls.Add(body)

        let top = stack (titleLabel title :: header)
        top.Dock <- DockStyle.Top
        top.Padding <- Padding(0, 0, 0, Dpi.px 12)

        // docking goes from the last added control inwards
        outer.Controls.Add(card)
        outer.Controls.Add(top)
        outer :> Control
