#r "nuget: PuppeteerSharp, 20.2.2"

open System
open System.Text
open System.Text.RegularExpressions
open PuppeteerSharp

// ---------------- Config ----------------
let mutable MAX_CHARS = 1200
let mutable HEAD = 900
let mutable TAIL = 250
let mutable INDENT = 2
let mutable USE_COLOR = true  // toggle on/off at runtime

// --------------- Helpers ----------------
let truncateMiddle (head:int) (tail:int) (s:string) =
    if String.IsNullOrEmpty s || s.Length <= head + tail + 10 then s
    else s.Substring(0, head) + "\n…\n" + s.Substring(s.Length - tail)

let voidTags =
    set [ "area"; "base"; "br"; "col"; "embed"; "hr"; "img"; "input"; "link"
          "meta"; "param"; "source"; "track"; "wbr" ]

let isClosingTag (t:string) = t.StartsWith("</")
let tagName (t:string) =
    let m = Regex.Match(t, @"^</?\s*([a-zA-Z0-9:-]+)")
    if m.Success then m.Groups.[1].Value.ToLowerInvariant() else ""

let isSelfClosing (t:string) =
    t.EndsWith("/>") || t.StartsWith("<!--") || t.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)

let prettyHtml (html:string) =
    if String.IsNullOrWhiteSpace html then html else
    let tokens =
        Regex.Split(html, @"(?=<)")
        |> Array.collect (fun chunk ->
            if chunk.StartsWith("<") then Regex.Split(chunk, @"(?<=>)")
            else [| chunk |])
        |> Array.filter (fun t -> t <> "")

    let mutable indent = 0
    let sb = StringBuilder()
    let appendLine i (text:string) =
        let trimmed = text.Trim()
        if trimmed <> "" then
            sb.Append(' ', i * INDENT).AppendLine(trimmed) |> ignore

    for t in tokens do
        if t.StartsWith("<") && t.EndsWith(">") then
            let tn = tagName t
            let closing = isClosingTag t
            let selfClosing = isSelfClosing t || voidTags.Contains tn
            if closing then indent <- Math.Max(0, indent - 1)
            appendLine indent t
            if (not closing) && (not selfClosing) then indent <- indent + 1
        else
            let text = Regex.Replace(t, @"\s+", " ").Trim()
            if text <> "" then appendLine indent text

    sb.ToString().TrimEnd()

// --------- Colorizer (ANSI) ----------
let esc s = "\u001b[" + s + "m"
let RESET  = esc "0"
let DIM    = esc "2"
let ITALIC = esc "3"
let GRAY   = esc "90"
let RED    = esc "31"
let GREEN  = esc "32"
let YELLOW = esc "33"
let BLUE   = esc "34"
let MAGENTA= esc "35"
let CYAN   = esc "36"

let inline color (c:string) (s:string) = if USE_COLOR then c + s + RESET else s

let colorizeAttributes (attrs:string) =
    // highlight attr names and values: name=, "value"
    // name
    let s1 = Regex.Replace(attrs, @"\b([a-zA-Z_:][-a-zA-Z0-9_:.]*)\b(?=\s*=)", fun (m:Match) ->
        color YELLOW m.Groups.[1].Value
    )
    // "value" or 'value' or bareword
    Regex.Replace(s1, @"=\s*(""[^""]*""|'[^']*'|[^\s>]+)", fun (m:Match) ->
        let v = m.Groups.[1].Value
        "=" + (color GREEN v)
    )

let colorizeTag (t:string) =
    if t.StartsWith("<!--") then
        color (GRAY + DIM) t
    elif t.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) then
        color MAGENTA t
    else
        // < / tag attrs >
        let m = Regex.Match(t, @"^<\s*(/)?\s*([a-zA-Z0-9:-]+)([^>]*)>")
        if not m.Success then t else
        let slash = if String.IsNullOrEmpty m.Groups.[1].Value then "" else "/"
        let tn = m.Groups.[2].Value
        let attrs = m.Groups.[3].Value
        let angledOpen = color GRAY "<"
        let angledClose = color GRAY ">"
        let slashCol = if slash = "" then "" else color GRAY "/"
        let tagCol = color CYAN tn
        let attrsCol = if String.IsNullOrWhiteSpace attrs then "" else colorizeAttributes attrs
        // self-closing "/>"
        if t.EndsWith("/>") then
            sprintf "%s%s%s%s%s" angledOpen slashCol tagCol attrsCol (color GRAY "/>")
        else
            sprintf "%s%s%s%s%s" angledOpen slashCol tagCol attrsCol angledClose

let colorizeHtmlLine (line:string) =
    if not USE_COLOR then line
    elif line.StartsWith("<") && line.EndsWith(">") then colorizeTag line
    else color DIM line

let colorizeHtmlBlock (pretty:string) =
    if not USE_COLOR then pretty
    else
        pretty.Split([|'\n'|]) |> Array.map colorizeHtmlLine |> String.concat "\n"

// Grab outerHTML in the page; supports Document and Element nodes
let outerHtmlEval = """
el => {
  try {
    if (el instanceof Document) {
      return el.documentElement ? el.documentElement.outerHTML : '';
    }
    if (el && el.outerHTML !== undefined) return el.outerHTML;
    const s = new XMLSerializer();
    return s.serializeToString(el);
  } catch (e) {
    return '';
  }
}
"""

// ------------- Printers -----------------

fsi.AddPrinter(fun (eh: ElementHandle) ->
    try
        let raw = eh.EvaluateFunctionAsync<string>(outerHtmlEval).Result
        if String.IsNullOrWhiteSpace raw then "ElementHandle(<no outerHTML>)"
        else
            let pretty = prettyHtml raw
            let shown = if pretty.Length > MAX_CHARS then truncateMiddle HEAD TAIL pretty else pretty
            let colored = colorizeHtmlBlock shown
            "ElementHandle outerHTML:\n" + colored
    with _ ->
        "ElementHandle(<unavailable>)"
)

fsi.AddPrinter(fun (h: JSHandle) ->
    match h with
    | :? ElementHandle as eh ->
        try
            let raw = eh.EvaluateFunctionAsync<string>(outerHtmlEval).Result
            if String.IsNullOrWhiteSpace raw then "ElementHandle(<no outerHTML>)"
            else
                let pretty = prettyHtml raw
                let shown = if pretty.Length > MAX_CHARS then truncateMiddle HEAD TAIL pretty else pretty
                let colored = colorizeHtmlBlock shown
                "ElementHandle outerHTML:\n" + colored
        with _ -> "ElementHandle(<unavailable>)"
    | _ ->
        try
            let v = h.JsonValueAsync<obj>().Result
            let json = System.Text.Json.JsonSerializer.Serialize(v)
            if json.Length > MAX_CHARS then truncateMiddle HEAD TAIL json else json
        with _ ->
            $"JSHandle({h.GetType().Name})"
)

// // ---- Optional: small helpers to tweak options at runtime ----
// let setPreviewOptions (?maxChars:int, ?head:int, ?tail:int, ?indent:int, ?useColor:bool) =
//     maxChars |> Option.iter (fun v -> MAX_CHARS <- Math.Max(200, v))
//     head     |> Option.iter (fun v -> HEAD <- Math.Max(100, v))
//     tail     |> Option.iter (fun v -> TAIL <- Math.Max(80, v))
//     indent   |> Option.iter (fun v -> INDENT <- Math.Max(1, v))
//     useColor |> Option.iter (fun v -> USE_COLOR <- v)
