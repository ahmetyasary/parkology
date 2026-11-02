// Parkology 2D art: logo, app icon, launch background, UI sprites and icons.
// Run from the project root:  swift Tools/generate-art.swift
// Everything is drawn with CoreGraphics paths; text uses the bundled Rammetto One font (OFL).
import AppKit
import CoreGraphics
import CoreText
import Foundation

let outDir = URL(fileURLWithPath: "Assets/Resources/Art", isDirectory: true)
let fontURL = URL(fileURLWithPath: "Assets/Resources/Fonts/RammettoOne-Regular.ttf")
try? FileManager.default.createDirectory(at: outDir, withIntermediateDirectories: true)
CTFontManagerRegisterFontsForURL(fontURL as CFURL, .process, nil)

// MARK: - helpers

func rgb(_ hex: UInt32, _ a: CGFloat = 1) -> CGColor {
    CGColor(srgbRed: CGFloat((hex >> 16) & 0xFF) / 255, green: CGFloat((hex >> 8) & 0xFF) / 255, blue: CGFloat(hex & 0xFF) / 255, alpha: a)
}

func shade(_ c: CGColor, _ k: CGFloat) -> CGColor {
    let p = c.components!
    if k < 1 { return CGColor(srgbRed: p[0] * k, green: p[1] * k, blue: p[2] * k, alpha: p[3]) }
    let t = k - 1
    return CGColor(srgbRed: p[0] + (1 - p[0]) * t, green: p[1] + (1 - p[1]) * t, blue: p[2] + (1 - p[2]) * t, alpha: p[3])
}

/// Draws into a top-left-origin context and writes a PNG.
func render(_ name: String, _ w: Int, _ h: Int, _ draw: (CGContext) -> Void) {
    let cs = CGColorSpace(name: CGColorSpace.sRGB)!
    let ctx = CGContext(data: nil, width: w, height: h, bitsPerComponent: 8, bytesPerRow: 0, space: cs, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    ctx.translateBy(x: 0, y: CGFloat(h))
    ctx.scaleBy(x: 1, y: -1)
    ctx.setShouldAntialias(true)
    ctx.interpolationQuality = .high
    draw(ctx)
    let image = ctx.makeImage()!
    let rep = NSBitmapImageRep(cgImage: image)
    try! rep.representation(using: .png, properties: [:])!.write(to: outDir.appendingPathComponent(name + ".png"))
    print("wrote", name, w, h)
}

func rounded(_ r: CGRect, _ radius: CGFloat) -> CGPath {
    CGPath(roundedRect: r, cornerWidth: min(radius, r.width / 2), cornerHeight: min(radius, r.height / 2), transform: nil)
}

func fill(_ ctx: CGContext, _ path: CGPath, _ color: CGColor) {
    ctx.addPath(path); ctx.setFillColor(color); ctx.fillPath()
}

func stroke(_ ctx: CGContext, _ path: CGPath, _ color: CGColor, _ width: CGFloat) {
    ctx.addPath(path); ctx.setStrokeColor(color); ctx.setLineWidth(width); ctx.setLineJoin(.round); ctx.setLineCap(.round); ctx.strokePath()
}

func vGradient(_ ctx: CGContext, _ path: CGPath, _ top: CGColor, _ bottom: CGColor, _ rect: CGRect) {
    ctx.saveGState()
    ctx.addPath(path); ctx.clip()
    let g = CGGradient(colorsSpace: CGColorSpace(name: CGColorSpace.sRGB), colors: [top, bottom] as CFArray, locations: [0, 1])!
    ctx.drawLinearGradient(g, start: CGPoint(x: rect.midX, y: rect.minY), end: CGPoint(x: rect.midX, y: rect.maxY), options: [])
    ctx.restoreGState()
}

func font(_ size: CGFloat) -> CTFont { CTFontCreateWithName("RammettoOne-Regular" as CFString, size, nil) }

/// Text path centred at a point (top-left coordinate system).
func textPath(_ s: String, _ size: CGFloat, center: CGPoint) -> CGPath {
    let attr = NSAttributedString(string: s, attributes: [.font: font(size)])
    let line = CTLineCreateWithAttributedString(attr)
    let runs = CTLineGetGlyphRuns(line) as! [CTRun]
    let path = CGMutablePath()
    let bounds = CTLineGetBoundsWithOptions(line, .useGlyphPathBounds)
    for run in runs {
        let count = CTRunGetGlyphCount(run)
        var glyphs = [CGGlyph](repeating: 0, count: count)
        var positions = [CGPoint](repeating: .zero, count: count)
        CTRunGetGlyphs(run, CFRange(location: 0, length: count), &glyphs)
        CTRunGetPositions(run, CFRange(location: 0, length: count), &positions)
        let runFont = (CTRunGetAttributes(run) as NSDictionary)[kCTFontAttributeName as String] as! CTFont
        for i in 0..<count {
            guard let g = CTFontCreatePathForGlyph(runFont, glyphs[i], nil) else { continue }
            // flip glyph (font y-up) into our y-down space
            let t = CGAffineTransform(translationX: center.x - bounds.midX + positions[i].x, y: center.y + bounds.midY).scaledBy(x: 1, y: -1)
            path.addPath(g, transform: t)
        }
    }
    return path
}

func outlinedText(_ ctx: CGContext, _ s: String, _ size: CGFloat, center: CGPoint, fillColor: CGColor, outline: CGColor, outlineWidth: CGFloat, shadow: CGFloat = 0) {
    let p = textPath(s, size, center: center)
    if shadow > 0 {
        var t = CGAffineTransform(translationX: 0, y: shadow)
        if let sp = p.copy(using: &t) {
            stroke(ctx, sp, outline, outlineWidth)
            fill(ctx, sp, outline)
        }
    }
    stroke(ctx, p, outline, outlineWidth)
    fill(ctx, p, fillColor)
}

// MARK: - palette

let red = rgb(0xF2474D), orange = rgb(0xFF9135), yellow = rgb(0xFFC83A), green = rgb(0x3DCB6C)
let blue = rgb(0x3F8CFF), purple = rgb(0x9A5CF5), pink = rgb(0xFF6EB4), teal = rgb(0x23C4C4)
let ink = rgb(0x1F2550), cream = rgb(0xFFF7EA), white = rgb(0xFFFFFF)
let skyTop = rgb(0x6FD6FF), skyBottom = rgb(0x4867F0)

// MARK: - brick buddy (used by icon and logo)

/// A front-facing 3/4 brick character: top face with studs, front face with a face, right side.
func brickBuddy(_ ctx: CGContext, x: CGFloat, y: CGFloat, w: CGFloat, h: CGFloat, color: CGColor, depth: CGFloat, outline: CGFloat) {
    let d = depth
    let front = CGRect(x: x, y: y + d, width: w, height: h)
    let r = w * 0.12
    // side and top as skewed rounded shapes
    let top = CGMutablePath()
    top.move(to: CGPoint(x: x + r, y: y + d)); top.addLine(to: CGPoint(x: x + d + r, y: y))
    top.addLine(to: CGPoint(x: x + w + d - r, y: y)); top.addQuadCurve(to: CGPoint(x: x + w + d, y: y + r * 0.6), control: CGPoint(x: x + w + d, y: y))
    top.addLine(to: CGPoint(x: x + w, y: y + d + r)); top.addLine(to: CGPoint(x: x + w - r, y: y + d)); top.closeSubpath()
    let side = CGMutablePath()
    side.move(to: CGPoint(x: x + w - 2, y: y + d + r)); side.addLine(to: CGPoint(x: x + w + d, y: y + r * 0.6))
    side.addLine(to: CGPoint(x: x + w + d, y: y + h - r)); side.addQuadCurve(to: CGPoint(x: x + w + d - r * 0.5, y: y + h), control: CGPoint(x: x + w + d, y: y + h))
    side.addLine(to: CGPoint(x: x + w - 2, y: y + d + h - r)); side.closeSubpath()
    let silhouette = CGMutablePath()
    silhouette.addPath(top); silhouette.addPath(side); silhouette.addPath(rounded(front, r))
    stroke(ctx, silhouette, ink, outline * 2)
    fill(ctx, side, shade(color, 0.72))
    fill(ctx, top, shade(color, 1.18))
    // studs on top (2 x 2)
    for row in 0..<2 {
        for col in 0..<2 {
            let t = CGFloat(row) / 1 * 0.5 + 0.25
            let cx = x + w * (0.27 + 0.46 * CGFloat(col)) + d * (1 - t)
            let cy = y + d * t
            let sw = w * 0.2, sh = sw * 0.45, stud = sw * 0.28
            let base = CGRect(x: cx - sw / 2, y: cy - sh / 2, width: sw, height: sh)
            let body = CGMutablePath()
            body.addRect(CGRect(x: base.minX, y: base.midY - stud, width: sw, height: stud))
            fill(ctx, CGPath(ellipseIn: base, transform: nil), shade(color, 0.8))
            fill(ctx, body, shade(color, 0.85))
            let cap = base.offsetBy(dx: 0, dy: -stud)
            fill(ctx, CGPath(ellipseIn: cap, transform: nil), shade(color, 1.3))
            stroke(ctx, CGPath(ellipseIn: cap, transform: nil), shade(color, 0.75), outline * 0.35)
        }
    }
    vGradient(ctx, rounded(front, r), shade(color, 1.08), shade(color, 0.92), front)
    // gloss
    fill(ctx, rounded(CGRect(x: x + w * 0.08, y: y + d + h * 0.07, width: w * 0.5, height: h * 0.1), h * 0.05), rgb(0xFFFFFF, 0.35))
    face(ctx, CGRect(x: x, y: y + d + h * 0.08, width: w, height: h * 0.9), mood: 0)
}

/// Eyes, cheeks and smile; mood 0 = happy, 1 = joy (^ ^).
func face(_ ctx: CGContext, _ r: CGRect, mood: Int) {
    let eyeW = r.width * 0.16, eyeH = r.height * 0.3
    for side in [-1.0, 1.0] as [CGFloat] {
        let cx = r.midX + side * r.width * 0.2, cy = r.minY + r.height * 0.42
        if mood == 1 {
            let p = CGMutablePath()
            p.move(to: CGPoint(x: cx - eyeW * 0.6, y: cy + eyeH * 0.1)); p.addQuadCurve(to: CGPoint(x: cx + eyeW * 0.6, y: cy + eyeH * 0.1), control: CGPoint(x: cx, y: cy - eyeH * 0.55))
            stroke(ctx, p, ink, eyeW * 0.32)
        } else {
            let e = CGRect(x: cx - eyeW / 2, y: cy - eyeH / 2, width: eyeW, height: eyeH)
            fill(ctx, CGPath(ellipseIn: e, transform: nil), ink)
            fill(ctx, CGPath(ellipseIn: CGRect(x: e.minX + eyeW * 0.18, y: e.minY + eyeH * 0.12, width: eyeW * 0.42, height: eyeW * 0.42), transform: nil), white)
        }
        let cheek = CGRect(x: cx + side * r.width * 0.07 - eyeW * 0.6, y: cy + eyeH * 0.55, width: eyeW * 1.2, height: eyeH * 0.32)
        fill(ctx, CGPath(ellipseIn: cheek, transform: nil), rgb(0xFF5A8A, 0.45))
    }
    let mouth = CGMutablePath()
    let my = r.minY + r.height * 0.7
    mouth.move(to: CGPoint(x: r.midX - r.width * 0.11, y: my)); mouth.addQuadCurve(to: CGPoint(x: r.midX + r.width * 0.11, y: my), control: CGPoint(x: r.midX, y: my + r.height * 0.2))
    stroke(ctx, mouth, ink, r.width * 0.045)
}

// MARK: - logo

func drawLogo(_ ctx: CGContext, width: CGFloat, height: CGFloat) {
    let letters = Array("PARKOLOGY")
    let colors = [red, orange, yellow, green, blue, purple, pink, teal, red]
    let tile: CGFloat = width / 10.2
    let gap = tile * 0.08
    let total = CGFloat(letters.count) * tile + CGFloat(letters.count - 1) * gap
    var x = (width - total) / 2
    let baseY = height * 0.5
    for (i, ch) in letters.enumerated() {
        let rot = (i % 2 == 0 ? -1.0 : 1.0) * 0.05 as CGFloat
        let lift = (i % 2 == 0 ? -1.0 : 1.0) * tile * 0.06
        let cx = x + tile / 2, cy = baseY + lift
        ctx.saveGState()
        ctx.translateBy(x: cx, y: cy); ctx.rotate(by: rot); ctx.translateBy(x: -cx, y: -cy)
        let body = CGRect(x: x, y: cy - tile * 0.55, width: tile, height: tile * 1.12)
        let lip: CGFloat = tile * 0.12
        let c = colors[i]
        // studs peeking above the tile
        for s in [0.28, 0.72] as [CGFloat] {
            let stud = CGRect(x: x + tile * s - tile * 0.15, y: body.minY - tile * 0.13, width: tile * 0.3, height: tile * 0.2)
            fill(ctx, rounded(stud.insetBy(dx: -tile * 0.03, dy: -tile * 0.03), tile * 0.07), ink)
        }
        fill(ctx, rounded(body.offsetBy(dx: 0, dy: lip).insetBy(dx: -tile * 0.05, dy: -tile * 0.05), tile * 0.2), ink)
        fill(ctx, rounded(body.offsetBy(dx: 0, dy: lip), tile * 0.17), shade(c, 0.7))
        vGradient(ctx, rounded(body, tile * 0.17), shade(c, 1.12), shade(c, 0.95), body)
        for s in [0.28, 0.72] as [CGFloat] {
            let stud = CGRect(x: x + tile * s - tile * 0.15, y: body.minY - tile * 0.13, width: tile * 0.3, height: tile * 0.2)
            fill(ctx, rounded(stud, tile * 0.06), shade(c, 1.2))
        }
        fill(ctx, rounded(CGRect(x: x + tile * 0.1, y: body.minY + tile * 0.08, width: tile * 0.45, height: tile * 0.1), tile * 0.05), rgb(0xFFFFFF, 0.4))
        outlinedText(ctx, String(ch), tile * 0.78, center: CGPoint(x: cx, y: body.midY + tile * 0.04), fillColor: white, outline: ink, outlineWidth: tile * 0.11, shadow: tile * 0.06)
        ctx.restoreGState()
        x += tile + gap
    }
}

render("logo", 1600, 420) { ctx in drawLogo(ctx, width: 1600, height: 420) }

// MARK: - app icon

func drawIcon(_ ctx: CGContext, size s: CGFloat, legacy: Bool) {
    let full = CGRect(x: 0, y: 0, width: s, height: s)
    vGradient(ctx, CGPath(rect: full, transform: nil), skyTop, skyBottom, full)
    // light rays
    ctx.saveGState()
    for k in 0..<12 {
        let a = CGFloat(k) / 12 * .pi * 2
        let p = CGMutablePath()
        p.move(to: CGPoint(x: s / 2, y: s * 0.46))
        p.addLine(to: CGPoint(x: s / 2 + cos(a) * s, y: s * 0.46 + sin(a) * s))
        p.addLine(to: CGPoint(x: s / 2 + cos(a + 0.22) * s, y: s * 0.46 + sin(a + 0.22) * s))
        p.closeSubpath()
        fill(ctx, p, rgb(0xFFFFFF, 0.07))
    }
    ctx.restoreGState()
    // ground baseplate
    let ground = CGRect(x: 0, y: s * 0.78, width: s, height: s * 0.22)
    fill(ctx, CGPath(rect: ground, transform: nil), rgb(0x2FB463))
    fill(ctx, CGPath(rect: CGRect(x: 0, y: s * 0.78, width: s, height: s * 0.025), transform: nil), rgb(0x5EE08E))
    for i in 0..<9 {
        let cx = s * (0.06 + CGFloat(i) * 0.11)
        fill(ctx, CGPath(ellipseIn: CGRect(x: cx - s * 0.035, y: s * 0.85, width: s * 0.07, height: s * 0.03), transform: nil), rgb(0x56D987))
    }
    // parking sign
    let pole = CGRect(x: s * 0.13, y: s * 0.3, width: s * 0.03, height: s * 0.5)
    fill(ctx, rounded(pole, s * 0.01), rgb(0xD8DEEA))
    let sign = CGRect(x: s * 0.04, y: s * 0.12, width: s * 0.21, height: s * 0.21)
    fill(ctx, rounded(sign.insetBy(dx: -s * 0.012, dy: -s * 0.012), s * 0.06), white)
    fill(ctx, rounded(sign, s * 0.05), rgb(0x2E6BEA))
    outlinedText(ctx, "P", s * 0.16, center: CGPoint(x: sign.midX, y: sign.midY + s * 0.005), fillColor: white, outline: rgb(0x1D47A8), outlineWidth: s * 0.012)
    // shadow + buddy
    fill(ctx, CGPath(ellipseIn: CGRect(x: s * 0.24, y: s * 0.79, width: s * 0.6, height: s * 0.07), transform: nil), rgb(0x000000, 0.22))
    brickBuddy(ctx, x: s * 0.24, y: s * 0.3, w: s * 0.5, h: s * 0.38, color: red, depth: s * 0.12, outline: s * 0.012)
    // stacked small yellow brick on top for the "stacking" idea
    brickBuddy(ctx, x: s * 0.4, y: s * 0.13, w: s * 0.26, h: s * 0.15, color: yellow, depth: s * 0.06, outline: s * 0.008)
}

render("app-icon", 1024, 1024) { ctx in drawIcon(ctx, size: 1024, legacy: false) }

// MARK: - launch / splash background

func drawSky(_ ctx: CGContext, w: CGFloat, h: CGFloat) {
    let r = CGRect(x: 0, y: 0, width: w, height: h)
    vGradient(ctx, CGPath(rect: r, transform: nil), skyTop, skyBottom, r)
    // soft stud dots pattern
    let step = w / 9
    var row = 0
    var y = step * 0.5
    while y < h {
        var x = (row % 2 == 0 ? step * 0.5 : step)
        while x < w + step {
            fill(ctx, CGPath(ellipseIn: CGRect(x: x - step * 0.16, y: y - step * 0.16, width: step * 0.32, height: step * 0.32), transform: nil), rgb(0xFFFFFF, 0.06))
            x += step
        }
        y += step * 0.9; row += 1
    }
}

render("splash-bg", 1080, 2340) { ctx in drawSky(ctx, w: 1080, h: 2340) }
render("launch-logo", 1240, 330) { ctx in ctx.scaleBy(x: 1240.0 / 1600.0, y: 1240.0 / 1600.0); drawLogo(ctx, width: 1600, height: 420) }

// MARK: - UI sprites (9-sliced in Unity; borders are set by the importer)

func button(_ name: String, _ c: CGColor) {
    render(name, 240, 160) { ctx in
        let body = CGRect(x: 8, y: 8, width: 224, height: 132)
        fill(ctx, rounded(CGRect(x: 4, y: 6, width: 232, height: 150), 52), ink)
        fill(ctx, rounded(CGRect(x: 8, y: 14, width: 224, height: 138), 48), shade(c, 0.68))
        vGradient(ctx, rounded(body, 48), shade(c, 1.15), shade(c, 0.95), body)
        fill(ctx, rounded(CGRect(x: 30, y: 20, width: 180, height: 30), 15), rgb(0xFFFFFF, 0.35))
    }
}
button("btn-green", green)
button("btn-blue", blue)
button("btn-red", red)
button("btn-orange", orange)
button("btn-yellow", yellow)
button("btn-purple", purple)
button("btn-grey", rgb(0xA9B2C7))

render("circle-btn", 220, 230) { ctx in
    fill(ctx, CGPath(ellipseIn: CGRect(x: 4, y: 12, width: 212, height: 212), transform: nil), ink)
    fill(ctx, CGPath(ellipseIn: CGRect(x: 10, y: 20, width: 200, height: 200), transform: nil), rgb(0x2A4FC4))
    let face = CGRect(x: 10, y: 8, width: 200, height: 200)
    vGradient(ctx, CGPath(ellipseIn: face, transform: nil), rgb(0x67A6FF), rgb(0x3A72F0), face)
    fill(ctx, CGPath(ellipseIn: CGRect(x: 40, y: 24, width: 140, height: 50), transform: nil), rgb(0xFFFFFF, 0.3))
}

render("panel", 200, 200) { ctx in
    fill(ctx, rounded(CGRect(x: 2, y: 2, width: 196, height: 196), 56), ink)
    fill(ctx, rounded(CGRect(x: 10, y: 10, width: 180, height: 180), 48), rgb(0xE7D6BD))
    fill(ctx, rounded(CGRect(x: 10, y: 10, width: 180, height: 172), 48), cream)
}

render("panel-header", 200, 140) { ctx in
    let r = CGRect(x: 2, y: 2, width: 196, height: 136)
    fill(ctx, rounded(r, 56), ink)
    let inner = r.insetBy(dx: 8, dy: 8)
    vGradient(ctx, rounded(inner, 48), rgb(0x5C8DFF), rgb(0x3557D8), inner)
    fill(ctx, rounded(CGRect(x: 30, y: 16, width: 140, height: 22), 11), rgb(0xFFFFFF, 0.25))
}

render("card", 120, 120) { ctx in
    fill(ctx, rounded(CGRect(x: 0, y: 0, width: 120, height: 120), 34), rgb(0xF3E6D1))
    fill(ctx, rounded(CGRect(x: 0, y: 0, width: 120, height: 114), 34), rgb(0xFFFDF8))
}

render("pill", 200, 100) { ctx in
    fill(ctx, rounded(CGRect(x: 2, y: 4, width: 196, height: 92), 46), ink)
    fill(ctx, rounded(CGRect(x: 8, y: 10, width: 184, height: 78), 39), rgb(0xFFF4DD))
}

render("bar-track", 120, 70) { ctx in
    fill(ctx, rounded(CGRect(x: 0, y: 0, width: 120, height: 70), 35), ink)
    fill(ctx, rounded(CGRect(x: 7, y: 7, width: 106, height: 56), 28), rgb(0x2B3470))
}

render("bar-fill", 100, 56) { ctx in
    let r = CGRect(x: 0, y: 0, width: 100, height: 56)
    vGradient(ctx, rounded(r, 28), rgb(0xFFE070), rgb(0xFFAA22), r)
    fill(ctx, rounded(CGRect(x: 14, y: 8, width: 72, height: 12), 6), rgb(0xFFFFFF, 0.45))
}

render("toggle-on", 160, 90) { ctx in
    fill(ctx, rounded(CGRect(x: 0, y: 0, width: 160, height: 90), 45), ink)
    fill(ctx, rounded(CGRect(x: 6, y: 6, width: 148, height: 78), 39), green)
}
render("toggle-off", 160, 90) { ctx in
    fill(ctx, rounded(CGRect(x: 0, y: 0, width: 160, height: 90), 45), ink)
    fill(ctx, rounded(CGRect(x: 6, y: 6, width: 148, height: 78), 39), rgb(0xC9CEDB))
}
render("knob", 90, 90) { ctx in
    fill(ctx, CGPath(ellipseIn: CGRect(x: 4, y: 6, width: 82, height: 82), transform: nil), rgb(0x000000, 0.25))
    fill(ctx, CGPath(ellipseIn: CGRect(x: 4, y: 2, width: 82, height: 82), transform: nil), white)
}

render("badge", 80, 80) { ctx in
    fill(ctx, CGPath(ellipseIn: CGRect(x: 2, y: 2, width: 76, height: 76), transform: nil), white)
    fill(ctx, CGPath(ellipseIn: CGRect(x: 8, y: 8, width: 64, height: 64), transform: nil), rgb(0xF23B4A))
}
render("badge-green", 80, 80) { ctx in
    fill(ctx, CGPath(ellipseIn: CGRect(x: 2, y: 2, width: 76, height: 76), transform: nil), white)
    fill(ctx, CGPath(ellipseIn: CGRect(x: 8, y: 8, width: 64, height: 64), transform: nil), rgb(0x2DBE5F))
}

render("tile", 64, 64) { ctx in fill(ctx, rounded(CGRect(x: 0, y: 0, width: 64, height: 64), 16), white) }
render("dot", 64, 64) { ctx in fill(ctx, CGPath(ellipseIn: CGRect(x: 0, y: 0, width: 64, height: 64), transform: nil), white) }
render("glow", 256, 256) { ctx in
    let g = CGGradient(colorsSpace: CGColorSpace(name: CGColorSpace.sRGB), colors: [rgb(0xFFFFFF, 0.9), rgb(0xFFFFFF, 0)] as CFArray, locations: [0, 1])!
    ctx.drawRadialGradient(g, startCenter: CGPoint(x: 128, y: 128), startRadius: 0, endCenter: CGPoint(x: 128, y: 128), endRadius: 128, options: [])
}
render("rays", 512, 512) { ctx in
    for k in 0..<14 {
        let a = CGFloat(k) / 14 * .pi * 2
        let p = CGMutablePath()
        p.move(to: CGPoint(x: 256, y: 256))
        p.addLine(to: CGPoint(x: 256 + cos(a) * 256, y: 256 + sin(a) * 256))
        p.addLine(to: CGPoint(x: 256 + cos(a + 0.2) * 256, y: 256 + sin(a + 0.2) * 256))
        p.closeSubpath()
        fill(ctx, p, rgb(0xFFFFFF, 0.5))
    }
}
render("vignette", 64, 64) { ctx in
    let g = CGGradient(colorsSpace: CGColorSpace(name: CGColorSpace.sRGB), colors: [rgb(0x000000, 0), rgb(0x000000, 0.55)] as CFArray, locations: [0.4, 1])!
    ctx.drawRadialGradient(g, startCenter: CGPoint(x: 32, y: 32), startRadius: 0, endCenter: CGPoint(x: 32, y: 32), endRadius: 46, options: [.drawsAfterEndLocation])
}

// MARK: - icons (white glyphs with dark outline, 160 px)

func icon(_ name: String, evenOdd: Bool = false, _ draw: (CGContext, CGMutablePath) -> Void) {
    render("icon-" + name, 160, 160) { ctx in
        let p = CGMutablePath()
        draw(ctx, p)
        stroke(ctx, p, ink, 22)
        ctx.addPath(p); ctx.setFillColor(white)
        ctx.fillPath(using: evenOdd ? .evenOdd : .winding)
    }
}

icon("gear", evenOdd: true) { _, p in
    let c = CGPoint(x: 80, y: 80)
    let teeth = 8
    for i in 0..<teeth * 2 {
        let a = CGFloat(i) / CGFloat(teeth * 2) * .pi * 2
        let r: CGFloat = i % 2 == 0 ? 62 : 50
        let a0 = a - .pi / CGFloat(teeth * 2) * 0.85, a1 = a + .pi / CGFloat(teeth * 2) * 0.85
        let p0 = CGPoint(x: c.x + cos(a0) * r, y: c.y + sin(a0) * r), p1 = CGPoint(x: c.x + cos(a1) * r, y: c.y + sin(a1) * r)
        if i == 0 { p.move(to: p0) } else { p.addLine(to: p0) }
        p.addLine(to: p1)
    }
    p.closeSubpath()
    p.addEllipse(in: CGRect(x: 58, y: 58, width: 44, height: 44))
}

icon("hammer") { _, p in
    var t = CGAffineTransform(translationX: 80, y: 80).rotated(by: -.pi / 4)
    p.addPath(rounded(CGRect(x: -10, y: -14, width: 20, height: 84), 10), transform: t)
    p.addPath(rounded(CGRect(x: -46, y: -58, width: 92, height: 46), 16), transform: t)
    _ = t; t = .identity
}

icon("magnet") { _, p in
    let m = CGMutablePath()
    m.move(to: CGPoint(x: 30, y: 40)); m.addLine(to: CGPoint(x: 30, y: 82))
    m.addArc(center: CGPoint(x: 80, y: 82), radius: 50, startAngle: .pi, endAngle: 0, clockwise: true)
    m.addLine(to: CGPoint(x: 130, y: 40)); m.addLine(to: CGPoint(x: 100, y: 40)); m.addLine(to: CGPoint(x: 100, y: 82))
    m.addArc(center: CGPoint(x: 80, y: 82), radius: 20, startAngle: 0, endAngle: .pi, clockwise: false)
    m.addLine(to: CGPoint(x: 60, y: 40)); m.closeSubpath()
    p.addPath(m)
}

icon("slot") { ctx, p in
    // little truck + plus
    p.addPath(rounded(CGRect(x: 18, y: 70, width: 100, height: 40), 12))
    p.addPath(rounded(CGRect(x: 86, y: 44, width: 40, height: 40), 10))
    p.addEllipse(in: CGRect(x: 30, y: 98, width: 28, height: 28))
    p.addEllipse(in: CGRect(x: 82, y: 98, width: 28, height: 28))
    p.addPath(rounded(CGRect(x: 30, y: 12, width: 14, height: 46), 7))
    p.addPath(rounded(CGRect(x: 14, y: 28, width: 46, height: 14), 7))
}

icon("home") { _, p in
    p.move(to: CGPoint(x: 80, y: 18)); p.addLine(to: CGPoint(x: 144, y: 74)); p.addLine(to: CGPoint(x: 124, y: 74))
    p.addLine(to: CGPoint(x: 124, y: 138)); p.addLine(to: CGPoint(x: 96, y: 138)); p.addLine(to: CGPoint(x: 96, y: 100))
    p.addLine(to: CGPoint(x: 64, y: 100)); p.addLine(to: CGPoint(x: 64, y: 138)); p.addLine(to: CGPoint(x: 36, y: 138))
    p.addLine(to: CGPoint(x: 36, y: 74)); p.addLine(to: CGPoint(x: 16, y: 74)); p.closeSubpath()
}

icon("restart") { _, p in
    let arc = CGMutablePath()
    arc.addArc(center: CGPoint(x: 80, y: 84), radius: 48, startAngle: -.pi * 0.35, endAngle: -.pi * 0.65 + .pi * 2 * 0.0, clockwise: false)
    let thick = arc.copy(strokingWithWidth: 22, lineCap: .round, lineJoin: .round, miterLimit: 4)
    p.addPath(thick)
    p.move(to: CGPoint(x: 96, y: 14)); p.addLine(to: CGPoint(x: 134, y: 40)); p.addLine(to: CGPoint(x: 96, y: 62)); p.closeSubpath()
}

icon("close") { _, p in
    let t = CGAffineTransform(translationX: 80, y: 80).rotated(by: .pi / 4)
    p.addPath(rounded(CGRect(x: -14, y: -58, width: 28, height: 116), 14), transform: t)
    p.addPath(rounded(CGRect(x: -58, y: -14, width: 116, height: 28), 14), transform: t)
}

icon("play") { _, p in
    let tri = CGMutablePath()
    tri.move(to: CGPoint(x: 50, y: 28)); tri.addLine(to: CGPoint(x: 132, y: 80)); tri.addLine(to: CGPoint(x: 50, y: 132)); tri.closeSubpath()
    p.addPath(tri.copy(strokingWithWidth: 16, lineCap: .round, lineJoin: .round, miterLimit: 4).union(tri))
}

icon("lock") { _, p in
    p.addPath(rounded(CGRect(x: 32, y: 70, width: 96, height: 72), 16))
    let shackle = CGMutablePath()
    shackle.addArc(center: CGPoint(x: 80, y: 66), radius: 30, startAngle: .pi, endAngle: 0, clockwise: false)
    p.addPath(shackle.copy(strokingWithWidth: 16, lineCap: .butt, lineJoin: .round, miterLimit: 4))
}

icon("sound") { _, p in
    p.move(to: CGPoint(x: 20, y: 60)); p.addLine(to: CGPoint(x: 50, y: 60)); p.addLine(to: CGPoint(x: 86, y: 28))
    p.addLine(to: CGPoint(x: 86, y: 132)); p.addLine(to: CGPoint(x: 50, y: 100)); p.addLine(to: CGPoint(x: 20, y: 100)); p.closeSubpath()
    for (r, w) in [(26.0, 12.0), (48.0, 12.0)] as [(CGFloat, CGFloat)] {
        let a = CGMutablePath()
        a.addArc(center: CGPoint(x: 92, y: 80), radius: r, startAngle: -.pi / 3.2, endAngle: .pi / 3.2, clockwise: false)
        p.addPath(a.copy(strokingWithWidth: w, lineCap: .round, lineJoin: .round, miterLimit: 4))
    }
}

icon("music") { _, p in
    p.addEllipse(in: CGRect(x: 22, y: 102, width: 44, height: 34))
    p.addEllipse(in: CGRect(x: 92, y: 88, width: 44, height: 34))
    p.addRect(CGRect(x: 54, y: 34, width: 12, height: 86))
    p.addRect(CGRect(x: 124, y: 22, width: 12, height: 86))
    let beam = CGMutablePath()
    beam.move(to: CGPoint(x: 54, y: 34)); beam.addLine(to: CGPoint(x: 136, y: 18)); beam.addLine(to: CGPoint(x: 136, y: 42)); beam.addLine(to: CGPoint(x: 54, y: 58)); beam.closeSubpath()
    p.addPath(beam)
}

icon("vibration") { _, p in
    p.addPath(rounded(CGRect(x: 52, y: 20, width: 56, height: 120), 14))
    for side in [-1.0, 1.0] as [CGFloat] {
        for k in 0..<2 {
            let x = 80 + side * (44 + CGFloat(k) * 18)
            let z = CGMutablePath()
            z.move(to: CGPoint(x: x, y: 52)); z.addLine(to: CGPoint(x: x + side * 7, y: 66)); z.addLine(to: CGPoint(x: x - side * 7, y: 80))
            z.addLine(to: CGPoint(x: x + side * 7, y: 94)); z.addLine(to: CGPoint(x: x, y: 108))
            p.addPath(z.copy(strokingWithWidth: 8, lineCap: .round, lineJoin: .round, miterLimit: 4))
        }
    }
}

icon("globe") { _, p in
    let ring = CGMutablePath()
    ring.addEllipse(in: CGRect(x: 22, y: 22, width: 116, height: 116))
    ring.addEllipse(in: CGRect(x: 52, y: 22, width: 56, height: 116))
    ring.move(to: CGPoint(x: 22, y: 80)); ring.addLine(to: CGPoint(x: 138, y: 80))
    ring.move(to: CGPoint(x: 80, y: 22)); ring.addLine(to: CGPoint(x: 80, y: 138))
    p.addPath(ring.copy(strokingWithWidth: 12, lineCap: .round, lineJoin: .round, miterLimit: 4))
}

icon("star") { _, p in
    for i in 0..<10 {
        let a = -CGFloat.pi / 2 + CGFloat(i) * .pi / 5
        let r: CGFloat = i % 2 == 0 ? 66 : 30
        let pt = CGPoint(x: 80 + cos(a) * r, y: 84 + sin(a) * r)
        if i == 0 { p.move(to: pt) } else { p.addLine(to: pt) }
    }
    p.closeSubpath()
}

icon("check") { _, p in
    let c = CGMutablePath()
    c.move(to: CGPoint(x: 30, y: 84)); c.addLine(to: CGPoint(x: 66, y: 118)); c.addLine(to: CGPoint(x: 132, y: 44))
    p.addPath(c.copy(strokingWithWidth: 26, lineCap: .round, lineJoin: .round, miterLimit: 4))
}

icon("plus") { _, p in
    p.addPath(rounded(CGRect(x: 66, y: 24, width: 28, height: 112), 14))
    p.addPath(rounded(CGRect(x: 24, y: 66, width: 112, height: 28), 14))
}

// Coin: gold with a stud-like rim and "P".
render("coin", 160, 160) { ctx in
    fill(ctx, CGPath(ellipseIn: CGRect(x: 6, y: 10, width: 148, height: 146), transform: nil), ink)
    fill(ctx, CGPath(ellipseIn: CGRect(x: 14, y: 18, width: 132, height: 132), transform: nil), rgb(0xD98A00))
    let face = CGRect(x: 14, y: 12, width: 132, height: 130)
    vGradient(ctx, CGPath(ellipseIn: face, transform: nil), rgb(0xFFE36B), rgb(0xFFB21E), face)
    stroke(ctx, CGPath(ellipseIn: face.insetBy(dx: 16, dy: 16), transform: nil), rgb(0xE59A0B), 7)
    outlinedText(ctx, "P", 74, center: CGPoint(x: 80, y: 78), fillColor: rgb(0xFFF6C9), outline: rgb(0xB86E00), outlineWidth: 10)
}

// Hand pointer for the tutorial.
render("hand", 200, 220) { ctx in
    let p = CGMutablePath()
    p.addPath(rounded(CGRect(x: 70, y: 10, width: 40, height: 120), 20))
    p.addPath(rounded(CGRect(x: 104, y: 70, width: 36, height: 74), 18))
    p.addPath(rounded(CGRect(x: 134, y: 80, width: 34, height: 68), 17))
    p.addPath(rounded(CGRect(x: 160, y: 94, width: 30, height: 58), 15))
    p.addPath(rounded(CGRect(x: 66, y: 100, width: 124, height: 100), 40))
    p.addPath(rounded(CGRect(x: 30, y: 104, width: 70, height: 34), 17), transform: CGAffineTransform(rotationAngle: 0.5).concatenating(CGAffineTransform(translationX: 40, y: -10)))
    stroke(ctx, p, ink, 16)
    fill(ctx, p, white)
}

// MARK: - splash train (side view, facing right)

func studsRow(_ ctx: CGContext, x: CGFloat, y: CGFloat, w: CGFloat, count: Int, size: CGFloat, color: CGColor) {
    for i in 0..<count {
        let cx = x + w * (CGFloat(i) + 0.5) / CGFloat(count)
        let r = CGRect(x: cx - size / 2, y: y - size * 0.45, width: size, height: size * 0.55)
        fill(ctx, rounded(r.insetBy(dx: -5, dy: -5), size * 0.2), ink)
        fill(ctx, rounded(r, size * 0.16), shade(color, 1.15))
    }
}

func wheel(_ ctx: CGContext, _ c: CGPoint, _ r: CGFloat) {
    fill(ctx, CGPath(ellipseIn: CGRect(x: c.x - r - 5, y: c.y - r - 5, width: 2 * r + 10, height: 2 * r + 10), transform: nil), ink)
    fill(ctx, CGPath(ellipseIn: CGRect(x: c.x - r, y: c.y - r, width: 2 * r, height: 2 * r), transform: nil), rgb(0x3A4170))
    fill(ctx, CGPath(ellipseIn: CGRect(x: c.x - r * 0.42, y: c.y - r * 0.42, width: r * 0.84, height: r * 0.84), transform: nil), rgb(0xC9D2EA))
}

/// A brick buddy seen from the side-front: body with studs and a face.
func sideBuddy(_ ctx: CGContext, _ body: CGRect, _ color: CGColor, mood: Int) {
    studsRow(ctx, x: body.minX + body.width * 0.08, y: body.minY, w: body.width * 0.84, count: 2, size: body.width * 0.26, color: color)
    fill(ctx, rounded(body.insetBy(dx: -7, dy: -7), body.width * 0.2), ink)
    fill(ctx, rounded(body.offsetBy(dx: 0, dy: 6), body.width * 0.17), shade(color, 0.72))
    let top = CGRect(x: body.minX, y: body.minY, width: body.width, height: body.height - 6)
    vGradient(ctx, rounded(top, body.width * 0.17), shade(color, 1.12), shade(color, 0.96), top)
    fill(ctx, rounded(CGRect(x: body.minX + body.width * 0.1, y: body.minY + body.height * 0.08, width: body.width * 0.45, height: body.height * 0.09), 6), rgb(0xFFFFFF, 0.4))
    face(ctx, CGRect(x: body.minX, y: body.minY + body.height * 0.12, width: body.width, height: body.height * 0.85), mood: mood)
}

render("train-engine", 420, 330) { ctx in
    // chimney
    fill(ctx, rounded(CGRect(x: 268, y: 22, width: 64, height: 96), 14), ink)
    fill(ctx, rounded(CGRect(x: 276, y: 30, width: 48, height: 84), 10), rgb(0x3A4170))
    fill(ctx, rounded(CGRect(x: 258, y: 14, width: 84, height: 30), 12), ink)
    fill(ctx, rounded(CGRect(x: 266, y: 20, width: 68, height: 18), 8), rgb(0x56608F))
    // cab (back block)
    sideBuddy(ctx, CGRect(x: 26, y: 60, width: 160, height: 170), red, mood: 1)
    // boiler (front, with the main face)
    sideBuddy(ctx, CGRect(x: 170, y: 112, width: 220, height: 132), red, mood: 0)
    // chassis + wheels
    fill(ctx, rounded(CGRect(x: 14, y: 236, width: 396, height: 44), 18), ink)
    fill(ctx, rounded(CGRect(x: 22, y: 242, width: 380, height: 30), 12), rgb(0x2B3470))
    for x in [80.0, 200.0, 330.0] as [CGFloat] { wheel(ctx, CGPoint(x: x, y: 284), 34) }
}

let wagonColors: [(String, CGColor)] = [("blue", blue), ("yellow", yellow), ("green", green), ("purple", purple), ("orange", orange), ("pink", pink), ("teal", teal)]
for (i, (name, c)) in wagonColors.enumerated() {
    render("train-wagon-" + name, 300, 300) { ctx in
        sideBuddy(ctx, CGRect(x: 52, y: 70, width: 196, height: 150), c, mood: i % 3 == 1 ? 1 : 0)
        fill(ctx, rounded(CGRect(x: 14, y: 218, width: 272, height: 44), 18), ink)
        fill(ctx, rounded(CGRect(x: 22, y: 224, width: 256, height: 30), 12), rgb(0x2B3470))
        // coupling
        fill(ctx, rounded(CGRect(x: 0, y: 230, width: 22, height: 16), 6), ink)
        for x in [80.0, 220.0] as [CGFloat] { wheel(ctx, CGPoint(x: x, y: 266), 30) }
    }
}

render("rail", 200, 60) { ctx in
    for i in 0..<5 {
        fill(ctx, rounded(CGRect(x: CGFloat(i) * 40 + 8, y: 18, width: 24, height: 40), 6), rgb(0x8B5E3C))
    }
    fill(ctx, rounded(CGRect(x: 0, y: 10, width: 200, height: 16), 8), ink)
    fill(ctx, rounded(CGRect(x: 0, y: 12, width: 200, height: 8), 4), rgb(0xC9D2EA))
}
