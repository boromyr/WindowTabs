namespace Bemo
open System
open System.Drawing
open System.IO
open System.Windows.Forms
open Bemo.Win32
open Bemo.Win32.Forms
open System.Resources
open System.Reflection


type HotKeyView() =
    let settingsProperty name =
        {
            new IProperty<'a> with
                member x.value
                    with get() = unbox<'a>(Services.settings.getValue(name))
                    and set(value) = Services.settings.setValue(name, box(value))
        }

    let resources = new ResourceManager("Properties.Resources", Assembly.GetExecutingAssembly());

    let toggle key (prop:IProperty<bool>) =
        FluentUI.toggleCard (resources.GetString(key)) prop.value (fun value -> prop.value <- value)

    // items are (stored value, displayed text)
    let dropDown (prop:IProperty<string>, items: (string * string) list) =
        let combo = new FluentComboBox()
        combo.Font <- FluentTheme.Body
        combo.Width <- Dpi.px 160
        combo.Items.AddRange(items |> List.map snd |> List.toArray |> Array.map box)

        // select the stored value, or the first item
        let initialIndex =
            match items |> List.tryFindIndex (fst >> (=) prop.value) with
            | Some index -> index
            | None -> if combo.Items.Count > 0 then 0 else -1

        if initialIndex >= 0 then
            combo.SelectedIndex <- initialIndex

        combo.SelectedIndexChanged.Add(fun _ ->
            if combo.SelectedIndex >= 0 then
                prop.value <- fst (items.[combo.SelectedIndex])
        )

        combo :> Control

    let settingsDropDown key value = dropDown(settingsProperty(key), value)

    let card key control = FluentUI.settingCard (resources.GetString(key)) None control

    let hotKeyCard (key, text) =
        let editor = HotKeyEditor() :> IPropEditor
        editor.value <- Services.program.getHotKey(key)
        editor.changed.Add <| fun() ->
            Services.program.setHotKey key (unbox<int>(editor.value))
        card text editor.control

    let page =
        FluentUI.page (resources.GetString("Behavior")) [
            FluentUI.sectionHeader (FluentUI.text "SectionBasics")
            toggle "runAtStartup" (settingsProperty "runAtStartup")
            toggle "hideInactiveTabs" (settingsProperty "hideInactiveTabs")
            toggle "isTabbingEnabledForAllProcessesByDefault" (prop<IFilterService, bool>(Services.filter, "isTabbingEnabledForAllProcessesByDefault"))
            toggle "autoHide" (settingsProperty "autoHide")
            toggle "tabsInTitleBar" (settingsProperty "tabsInTitleBar")
            card "alignment" (settingsDropDown "alignment" [("Left", FluentUI.text "AlignLeft"); ("Center", FluentUI.text "AlignCenter"); ("Right", FluentUI.text "AlignRight")])

            FluentUI.settingCard (FluentUI.text "dragToGroupKey") (Some (FluentUI.text "dragToGroupKeyDescription"))
                (settingsDropDown "dragToGroupKey" [("Ctrl", FluentUI.text "KeyCtrl"); ("Shift", FluentUI.text "KeyShift"); ("Alt", FluentUI.text "KeyAlt"); ("None", FluentUI.text "Off")])

            FluentUI.sectionHeader (FluentUI.text "SectionTasks")
            toggle "combineIconsInTaskbar" (settingsProperty "combineIconsInTaskbar")
            toggle "replaceAltTab" (settingsProperty "replaceAltTab")
            toggle "groupWindowsInSwitcher" (settingsProperty "groupWindowsInSwitcher")

            FluentUI.sectionHeader (FluentUI.text "SectionSwitchTabs")
            toggle "enableCtrlNumberHotKey" (settingsProperty "enableCtrlNumberHotKey")
            toggle "enableHoverActivate" (settingsProperty "enableHoverActivate")
            toggle "enableShiftScroll" (settingsProperty "enableShiftScroll")
            hotKeyCard ("nextTab", "nextTab")
            hotKeyCard ("prevTab", "prevTab")
        ]

    interface ISettingsView with
        member x.key = SettingsViewType.HotKeySettings
        member x.title = resources.GetString("Behavior")
        member x.control = page
