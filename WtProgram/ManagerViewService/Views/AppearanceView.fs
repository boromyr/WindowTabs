namespace Bemo
open System
open System.Drawing
open System.IO
open System.Windows.Forms
open Bemo.Win32
open Bemo.Win32.Forms
open Microsoft.FSharp.Reflection
open System.Resources
open System.Reflection

type AppearanceProperty = {
    displayText : string
    key: string
    propertyType : AppearancePropertyType
    }

and AppearancePropertyType =
    | HotKeyProperty
    | IntProperty
    | ColorProperty

type AppearanceView() as this =
    let colorConfig key displayText =
        { displayText=displayText; key=key; propertyType=ColorProperty }

    let intConfig key displayText =
        { displayText=displayText; key=key; propertyType=IntProperty }

    let resources = new ResourceManager("Properties.Resources", Assembly.GetExecutingAssembly());

    // tabOverlap is left out: rounded tabs sit side by side, so it no longer has any effect
    let sizeProperties = List2([
        intConfig "tabHeight" "Height"
        intConfig "tabMaxWidth" "Max Width"
        intConfig "tabIndentNormal" "Indent Normal"
        intConfig "tabIndentFlipped" "Indent Flipped"
        ])

    let colorProperties = List2([
        colorConfig "tabTextColor" "Text Color"
        colorConfig "tabNormalBgColor" "Background Normal"
        colorConfig "tabHighlightBgColor" "Background Highlight"
        colorConfig "tabActiveBgColor" "Background Active"
        colorConfig "tabFlashBgColor" "Background Flash"
        colorConfig "tabBorderColor" "Border"
        ])

    let properties = sizeProperties.appendList(colorProperties)

    let editors = properties.fold (Map2()) <| fun editors prop ->
        let editor =
            match prop.propertyType with
            | ColorProperty -> ColorEditor() :> IPropEditor
            | IntProperty -> IntEditor() :> IPropEditor
            | HotKeyProperty -> HotKeyEditor() :> IPropEditor
        editors.add prop.key editor

    let card prop =
        let editor = editors.find prop.key
        FluentUI.settingCard (resources.GetString(prop.displayText)) None editor.control

    let setEditorValues appearance =
        properties.iter <| fun prop ->
            let editor = editors.find prop.key
            try
                editor.value <- Serialize.readField appearance prop.key
            with | _ ->()

    let appearance = Services.program.tabAppearanceInfo

    let applyPreset preset = fun () ->
        setEditorValues preset
        this.applyAppearance()

    let presets =
        FluentUI.row [
            FluentUI.button None (resources.GetString("DarkMode")) (applyPreset Services.program.darkModeTabAppearanceInfo)
            FluentUI.button None (resources.GetString("DarkModeBlue")) (applyPreset Services.program.darkModeBlueTabAppearanceInfo)
            FluentUI.button None (resources.GetString("Reset")) (applyPreset Services.program.defaultTabAppearanceInfo)
        ]

    let page =
        FluentUI.page (resources.GetString("Appearance")) [
            yield FluentUI.sectionHeader (FluentUI.text "SectionSize")
            yield! sizeProperties.list |> List.map card
            yield FluentUI.sectionHeader (FluentUI.text "SectionColors")
            yield! colorProperties.list |> List.map card
            yield FluentUI.sectionHeader (FluentUI.text "SectionPresets")
            yield FluentUI.settingCard (FluentUI.text "ApplyColorScheme") None presets
        ]

    do
        setEditorValues appearance
        editors.items.map(snd).iter <| fun editor ->
            editor.changed.Add <| fun() -> this.applyAppearance()

    member this.applyAppearance() =
        let appearance = properties.fold appearance <| fun appearance property ->
            let value = (editors.find property.key).value
            (Serialize.writeField appearance property.key value) :?> TabAppearanceInfo
        Services.settings.setValue("tabAppearance", box(appearance))

    interface ISettingsView with
        member x.key = SettingsViewType.AppearanceSettings
        member x.title = resources.GetString("Appearance")
        member x.control = page
