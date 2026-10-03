namespace Bemo
open System
open System.Drawing

type Tab = Tab of IntPtr

and TabInfo = {
    text: string
    iconSmall: Icon
    iconBig: Icon
    preview: unit -> Img
    isRenamed: bool
}

and TabDragInfo = {
    tab: Tab
    tabOffset: Pt
    tabInfo: TabInfo
    }

and TabStripPlacment = {
    showInside: bool
    // drawn over the window's own title bar
    inTitleBar: bool
    bounds: Rect
    }

and TabPart =
    | TabBackground
    | TabIcon
    | TabClose

and TabDirection =
    | TabUp
    | TabDown

and TabAlignment =
    | TabLeft
    | TabCenter
    | TabRight

and TabDock =
    | TabDockTop
    | TabDockBottom
    | TabDockLeft
    | TabDockRight

and TabAppearanceInfo = {
    tabHeight: int
    tabMaxWidth: int
    tabOverlap: int
    tabTextColor : Color
    tabNormalBgColor: Color
    tabHighlightBgColor: Color
    tabActiveBgColor: Color
    tabFlashBgColor: Color
    tabBorderColor: Color
    tabHeightOffset : int
    tabIndentFlipped : int
    tabIndentNormal : int
    } with
    // sizes are stored in 96-DPI units; this converts them to physical pixels
    member this.scaled =
        { this with
            tabHeight = Dpi.px this.tabHeight
            tabMaxWidth = Dpi.px this.tabMaxWidth
            tabHeightOffset = Dpi.px this.tabHeightOffset
            tabIndentFlipped = Dpi.px this.tabIndentFlipped
            tabIndentNormal = Dpi.px this.tabIndentNormal }
