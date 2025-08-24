#r "nuget: PuppeteerSharp"

open System
open System.Text.RegularExpressions
open PuppeteerSharp

// ---------------- Config ----------------
let MAX_CHARS = 1200        // total characters to show
let HEAD = 900              // show first HEAD chars
let TAIL = 250              // and last TAIL chars
let INDENT = 2              // spaces per indent level

// --------------- Helpers ----------------
let truncateMiddle (head:int) (tail:int) (s:string) =
    if String.IsNullOrEmpty s || s.Length <= head + tail + 10 then s
    else s.Substring(0, head) + "\n…\n" + s.Substring(s.Length - tail)

let voidTags =
    set [ "area"; "base"; "br"; "col"; "embed"; "hr"; "img"; "input"; "link"
          "meta"; "param"; "source"; "track"; "wbr" ]

let isClosingTag (t:string) = t.StartsWith("</")
let tagName (t:string) =
    // crude but effective for pretty-printing
    let m = Regex.Match(t, @"^</?\s*([a-zA-Z0-9:-]+)")
    if m.Success then m.Groups.[1].Value.ToLowerInvariant() else ""

let isSelfClosing (t:string) =
    // <foo/> or <!----> treated as self-contained
    t.EndsWith("/>") || t.StartsWith("<!--") || t.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)

/// Very small HTML pretty-printer: splits by tags and indents block-wise.
/// Not a full HTML parser, but good enough for readable previews.
let prettyHtml (html:string) =
    if String.IsNullOrWhiteSpace html then html else
    let tokens =
        Regex.Split(html, @"(?=<)")  // keep '<' at start of tokens
        |> Array.collect (fun chunk ->
            if chunk.StartsWith("<") then
                // split so every tag boundary is a token line-ish
                Regex.Split(chunk, @"(?<=>)")
            else [| chunk |])
        |> Array.filter (fun t -> t <> "")

    let mutable indent = 0
    let sb = System.Text.StringBuilder()

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
            // text node: collapse whitespace a bit
            let text = Regex.Replace(t, @"\s+", " ").Trim()
            if text <> "" then appendLine indent text

    sb.ToString().TrimEnd()

// Grab outerHTML in the page; supports Document and Element nodes
let outerHtmlEval = """
el => {
  try {
    if (el instanceof Document) {
      return el.documentElement ? el.documentElement.outerHTML : '';
    }
    if (el && el.outerHTML !== undefined) return el.outerHTML;
    // Fallback: serialize node (e.g., Text or Comment nodes)
    const s = new XMLSerializer();
    return s.serializeToString(el);
  } catch (e) {
    return '';
  }
}
"""

// ------------- Printers -----------------

// ElementHandle: pretty outerHTML (truncated)
fsi.AddPrinter(fun (eh: ElementHandle) ->
    try
        let raw = eh.EvaluateFunctionAsync<string>(outerHtmlEval).Result
        if String.IsNullOrWhiteSpace raw then "ElementHandle(<no outerHTML>)"
        else
            let pretty = prettyHtml raw
            let shown =
                if pretty.Length > MAX_CHARS then truncateMiddle HEAD TAIL pretty else pretty
            // Wrap it so it’s clear it’s HTML
            "ElementHandle outerHTML:\n" + shown
    with _ ->
        "ElementHandle(<unavailable>)"
)

// JSHandle fallback: if it’s an element, reuse the above; else default preview
fsi.AddPrinter(fun (h: JSHandle) ->
    match h with
    | :? ElementHandle as eh ->
        // Let the ElementHandle printer format it
        try
            let raw = eh.EvaluateFunctionAsync<string>(outerHtmlEval).Result
            if String.IsNullOrWhiteSpace raw then "ElementHandle(<no outerHTML>)"
            else
                let pretty = prettyHtml raw
                let shown =
                    if pretty.Length > MAX_CHARS then truncateMiddle HEAD TAIL pretty else pretty
                "ElementHandle outerHTML:\n" + shown
        with _ -> "ElementHandle(<unavailable>)"
    | _ ->
        // Non-element JS values: compact JSON preview
        try
            let v = h.JsonValueAsync<obj>().Result
            let json = System.Text.Json.JsonSerializer.Serialize(v)
            if json.Length > MAX_CHARS then truncateMiddle HEAD TAIL json else json
        with _ ->
            $"JSHandle({h.GetType().Name})"
)
