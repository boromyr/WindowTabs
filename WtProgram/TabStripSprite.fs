namespace Bemo
open System
open System.Drawing
open System.Drawing.Drawing2D
open System.Drawing.Imaging
open System.Windows.Forms

type IconSprite = {
    icon: Icon
    size: Sz
    } with
    interface ISprite with
        member this.image = 
            let bitmap = Img(this.size)
            let g = bitmap.graphics
            try
                // pick the icon image closest to the target size, then scale it to fit exactly
                use sized = new Icon(this.icon, this.size.Size)
                do g.InterpolationMode <- InterpolationMode.HighQualityBicubic
                do g.DrawIcon(sized, Rect(Pt.empty, this.size).Rectangle)
            with | e -> ()
            bitmap
        member this.children = List2()

type CloseButtonSprite = {
    hover: bool
    captured: bool
    size: Sz
    }
    with
    member private this.bgColor =
        match this.hover, this.captured with
        | true, true -> Some(Color.DimGray)
        | true, false -> Some(Color.DarkRed)
        | _ -> None
    member private this.penColor = if this.bgColor.IsSome then Color.White else Color.Gray
    member private this.pen = new Pen(this.penColor, float32(Dpi.px 2))
    interface ISprite with
        member this.image = 
            let crossOffest = Dpi.px 3
            let bitmap = Img(this.size)
            let g = bitmap.graphics
            g.FillEllipse(new SolidBrush(this.bgColor.def(Color.FromArgb(1, 1, 1, 1))), Rect(Pt.empty, this.size).Rectangle)
            g.DrawLine(this.pen, crossOffest, crossOffest, this.size.width - crossOffest, this.size.height - crossOffest)
            g.DrawLine(this.pen, crossOffest, this.size.height - crossOffest, this.size.width - crossOffest, crossOffest)
            bitmap
        member this.children = List2()

type TabDisplayInfo = {
    bgColor : Color option
    text: string
    textFont: Font
    textBrush: Brush
    icon: Icon
    }
    
type TabSprite<'id> = {
    id: 'id
    isTop: bool
    appearance: TabAppearanceInfo
    displayInfo: TabDisplayInfo
    size: Sz
    onlyIcon: bool
    direction: TabDirection
    hover: TabPart option
    captured: TabPart option
    } with

    member private this.iconSprite =
        {
            IconSprite.icon = this.displayInfo.icon
            size = this.iconSize
        } :> ISprite
    
    member private this.closeButtonSprite = 
        {
            CloseButtonSprite.size = this.closeButtonSize
            hover = this.hover = Some(TabClose)
            captured = this.captured = Some(TabClose)
        } :> ISprite
       
    // horizontal space between the tab border and its icon / close button
    member private this.edgeWidth = Dpi.px 8

    member private this.cornerRadius = min (float32(Dpi.px 6)) (float32(this.size.height) / 2.0f)

    member private this.bgBrush =
        let color = 
            match this.displayInfo.bgColor with
            | Some(color) -> color
            | None ->
                let active = this.appearance.tabActiveBgColor
                let inactive = this.appearance.tabNormalBgColor
                let highlight = this.appearance.tabHighlightBgColor
                if this.isTop then active
                elif this.hover.IsSome || this.captured.IsSome then highlight
                else inactive
        SolidBrush(color)

    member private this.borderPen = new Pen(new SolidBrush(this.appearance.tabBorderColor), 1.0f)

    member private this.borderPath =
        let path = new GraphicsPath()
        // inset by half a pixel so the 1px border isn't clipped at the bitmap edges
        let left, top = 0.5f, 0.5f
        let right = float32(this.size.width) - 0.5f
        let bottom = float32(this.size.height) - 0.5f
        let d = 2.0f * this.cornerRadius
        if d > 0.0f && right - left > d && bottom - top > d then
            do path.AddArc(left, top, d, d, 180.0f, 90.0f)
            do path.AddArc(right - d, top, d, d, 270.0f, 90.0f)
            do path.AddArc(right - d, bottom - d, d, d, 0.0f, 90.0f)
            do path.AddArc(left, bottom - d, d, d, 90.0f, 90.0f)
            do path.CloseFigure()
        else
            do path.AddRectangle(RectangleF(left, top, max 0.0f (right - left), max 0.0f (bottom - top)))
        path

    // shrink the icon on short tabs (e.g. inside a title bar) so it keeps some padding
    member private this.iconSize =
        let side = max (Dpi.px 10) (min (Dpi.px 16) (this.size.height - Dpi.px 8))
        Sz(side, side)

    member private this.iconLocation =
        let y = (this.size.height - this.iconSize.height) / 2
        Pt(this.edgeWidth, y)

    member private this.closeButtonSize = Sz(Dpi.px 13, Dpi.px 13)

    member private this.closeButtonLocation =
        let x = this.size.width - this.edgeWidth - this.closeButtonSize.width
        let y = (this.size.height - this.closeButtonSize.height) / 2
        Pt(x, y)

    member this.textLocation =
        let x = this.iconLocation.x + this.iconSize.width + Dpi.px 5
        Pt(x, 0)

    member this.textSize =
        let width = this.size.width - this.textLocation.x - this.edgeWidth - this.closeButtonSize.width
        let width = max 1 width
        Sz(width, this.size.height)

    member this.tabTextBrush =
        new SolidBrush(this.appearance.tabTextColor)

    member private this.textFormat =
        let format = new StringFormat()
        do format.LineAlignment <- StringAlignment.Center
        do format.Alignment <- StringAlignment.Near
        do format.Trimming <- StringTrimming.EllipsisCharacter
        do format.FormatFlags <- format.FormatFlags ||| StringFormatFlags.NoWrap
        format

    // true when the title doesn't fit and is drawn cut off with an ellipsis
    member this.isTextTruncated =
        if this.onlyIcon then false
        else
            use bmp = new Bitmap(1, 1)
            use g = Graphics.FromImage(bmp)
            use format = this.textFormat
            let size = g.MeasureString(this.displayInfo.text, this.displayInfo.textFont, PointF.Empty, format)
            size.Width > float32(this.textSize.width)

    interface ISprite with
        member this.image =
            let img = Img(this.size)
            let g = img.graphics
            do g.FillPath(this.bgBrush, this.borderPath)
            do g.DrawPath(this.borderPen, this.borderPath)
            if this.onlyIcon.not then
                //the text can't be drawn as a separate bitmap because clearcase fonts
                //can't be drawn by gdi+ to a transparent background, need to draw directly on the tab background
                let text = this.displayInfo.text
                let font = this.displayInfo.textFont
                let brush = this.tabTextBrush
                let format = this.textFormat
                let bounds = Rect(this.textLocation, this.textSize)
                do g.DrawString(text, font, brush, bounds.Rectangle.RectangleF, format)
            img
        member this.children = 
            List2([
                Some(this.iconLocation,this.iconSprite) 
                (if this.onlyIcon then None else Some(this.closeButtonLocation, this.closeButtonSprite))
                ]).choose(id)

type TabStripSprite<'id> when 'id : equality = {
    tabs: Map2<'id, TabDisplayInfo>
    appearance: TabAppearanceInfo
    hover: ('id * TabPart) option
    captured : ('id * TabPart) option
    lorder: List2<'id>
    zorder: List2<'id>
    size: Sz
    slide: ('id * int) option
    alignment: Bemo.TabAlignment
    direction: TabDirection
    transparent: bool
    onlyIcons: bool
    // space left of the first tab, painted opaque in leadingColor
    leading: int
    leadingColor: Color
    } with

    // rounded tabs sit side by side with a small gap instead of overlapping
    member private this.tabOverlap = -float(Dpi.px 3)
    member private this.tabMaxLen = float(this.appearance.tabMaxWidth)

    member private this.tabSprite (tab:'id) = this.tabRecord(tab) :> ISprite

    member private this.tabRecord (tab:'id) =
        {
            TabSprite.id = tab
            isTop =
                match this.zorder.tryHead with
                | Some(top) -> top = tab
                | None -> false 
            displayInfo = this.tabs.find(tab)
            appearance = this.appearance
            size = this.tabSize
            onlyIcon = this.onlyIcons
            direction = this.direction
            hover = 
                match this.hover with
                | Some(id, part) when id = tab -> Some(part)
                | _ -> None
            captured =
                match this.captured with
                | Some(id, part) when id = tab -> Some(part)
                | _ -> None
        }

    member this.isTabTextTruncated tab = this.tabRecord(tab).isTextTruncated

    member private this.count = this.lorder.length

    member private this.bgImage =
        let bgColor = 
            if this.transparent then Color.FromArgb(0, 0, 0, 0)
            else Color.FromArgb(1, 1, 1, 1) 
        let gr, img = 
            let sz = this.size
            if sz.isEmptyArea then
                let bmp = new Bitmap(1,1)
                let gr = Graphics.FromImage(bmp)
                do gr.SmoothingMode <- SmoothingMode.AntiAlias
                (gr, bmp)
            else
                let bmp = new Bitmap(sz.width, sz.height)
                let gr = Graphics.FromImage(bmp)
                do gr.SmoothingMode <- SmoothingMode.AntiAlias
                (gr, bmp)   
        let bounds = Rect(Pt(), this.size)
        do  gr.FillRectangle(new SolidBrush(bgColor), bounds.Rectangle)
        if this.leading > 0 then
            // also under the first tab's rounded corners, which would otherwise let it show through
            do gr.FillRectangle(new SolidBrush(this.leadingColor), Rectangle(0, 0, this.leading + Dpi.px 8, this.size.height))
        img.img

    member private this.tabLengthWithOverlap tabOverlap =
        let tsWidth = float(this.size.width - this.leading)
        let tsWidth =
            if this.count < 2 then tsWidth 
            else 
                let tsWidth = tsWidth + float(this.count - 1) * tabOverlap
                tsWidth / float(this.count)
        min tsWidth this.tabMaxLen

    member private this.tabLength = this.tabLengthWithOverlap this.tabOverlap

    member private this.tabOffset index =
        let tabOffset = this.tabLength - this.tabOverlap
        float(this.leading) + float(index) * tabOffset

    member private this.alignmentOffset =
        let lastIndex = this.count - 1
        let lastTabRight = this.tabOffset lastIndex + this.tabLength
        let widthOfEmptySpace = float(this.size.width) - lastTabRight
        match this.alignment with
        | TabLeft -> 0.0
        | TabCenter -> widthOfEmptySpace / 2.0
        | TabRight -> widthOfEmptySpace - 60.0
            
    member this.tabLocation tab =
        match this.slide with
        | Some(slideTab, x) when tab = slideTab-> 
            let bounds = (this.leading, this.size.width - int(this.tabLength))
            Pt(between bounds x, 1)
        | _ -> 
            let x = this.tabOffset (this.adjustedLorder.findIndex((=)tab))
            let x = x + this.alignmentOffset
            Pt(int(x), 1)

    member this.tabSize = Sz(int(this.tabLength), (this.size.height) - 2)

    member this.movedTab =
        match this.slide with
        | Some(tab, x) ->
            let index = 
                if this.count = 0 then 0
                else
                    let x = float(x)
                    let x = x - this.alignmentOffset - float(this.leading)
                    let mid = x + this.tabLength / 2.0
                    int((mid - this.tabOverlap / 2.0) / (this.tabLength - this.tabOverlap))
            Some(tab, index)
        | None -> None

    member this.adjustedLorder : List2<'id> =
        match this.movedTab with
        | Some(tab, index) -> this.lorder.move((=)tab, index)
        | None -> this.lorder

    
    member this.sprite =
        {
            new ISprite with
            member x.image = this.bgImage 
            member x.children = this.zorder.map <| fun (tab:'id) -> 
                (this.tabLocation tab, this.tabSprite(tab))
        }

    member this.renderTab tab = this.tabSprite(tab).render

    member this.render = this.sprite.render

    member this.tryHit pt = 
        let path = this.sprite.hit(pt)
        maybe {
            let! tab = path.tryPick <| fun sprite ->
                match sprite with
                | :? TabSprite<'id> as ts -> Some(ts.id)
                | _ -> None 
            let part : TabPart = 
                match path.head with
                | :? TabSprite<'id> -> TabBackground
                | :? IconSprite -> TabIcon
                | :? CloseButtonSprite -> TabClose
                | _ -> TabBackground
            return tab,part
        }
