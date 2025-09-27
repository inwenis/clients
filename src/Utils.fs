module Utils

open System
open System.Text
open System.Threading
open System.Threading.Tasks
open PuppeteerSharp
open PuppeteerSharp.Input
open System.Text.RegularExpressions
open System.IO


let sleep x =
    printfn "sleep %is" x
    x |> int64 |> TimeSpan.FromSeconds |> Thread.Sleep

let wait (t: Task) = t.Wait()

let runSync (t: Task<'a>) = t.Result

let clickElement (e: IElementHandle) = e.ClickAsync() |> wait

let clickSelector xpath (e: IElementHandle) =
    printfn "click \"%s\"" xpath
    e.WaitForSelectorAsync xpath |> runSync |> clickElement

let click (p: IPage) xpath =
    printfn "click p \"%s\"" xpath
    p.WaitForSelectorAsync xpath |> runSync |> clickElement

let clickOrContinue (p: IPage) xpath =
    printfn "clickOrContinue p \"%s\"" xpath
    try
        let op = new WaitForSelectorOptions()
        op.Timeout <- 5000
        p.WaitForSelectorAsync(xpath, options = op) |> runSync |> clickElement
    with
    // swallow all exceptions because clickOrContinue is used for example when
    // a popup may or may not appear
    | _ -> ()

let typet (p: IPage) xpath text =
    printfn "typet p \"%s\" ..." xpath
    p.WaitForSelectorAsync xpath |> runSync |> fun x -> x.TypeAsync text |> wait

let typeSlow (p: IPage) xpath text =
    printfn "typeSlow p \"%s\" ..." xpath
    let options = new TypeOptions()
    options.Delay <- TimeSpan.FromSeconds(seconds = 1).TotalMilliseconds |> int

    p.WaitForSelectorAsync(xpath)
    |> runSync
    |> fun x -> x.TypeAsync(text, options) |> wait

let goto (p: IPage) url =
    printfn "goto p \"%s\"" url
    p.GoToAsync(url) |> wait

let gotoWithCustomTimeOut (p: IPage) url (timeoutMs: int) =
    printfn "goto p \"%s\" (timeout=%i ms)" url timeoutMs
    p.GoToAsync(url, timeout = timeoutMs) |> wait

let queryAll (p: IPage) xpath =
    printfn "queryAll \"%s\"" xpath
    p.QuerySelectorAllAsync xpath |> runSync

let queryFirst (p: IPage) xpath =
    printfn "querySingle \"%s\"" xpath
    p.QuerySelectorAsync xpath |> runSync

let queryElementAll (e: IElementHandle) xpath =
    printfn "queryElementAll e \"%s\"" xpath
    e.QuerySelectorAllAsync xpath |> runSync

let queryElementSingle (e: IElementHandle) xpath =
    printfn "querySingle \"%s\"" xpath
    e.QuerySelectorAsync xpath |> runSync

let waitSelector (p: IPage) xpath =
    printfn "waitSelector \"%s\"" xpath
    p.WaitForSelectorAsync xpath |> runSync

let getText (e: IElementHandle) =
    e.GetPropertyAsync("textContent").Result |> string

let getAttributeNames = fun (d:IElementHandle) -> d.EvaluateFunctionAsync<string[]> "node => Array.from(node.attributes).map(x => x.name)" |> runSync
let getAttributeValue = fun name (d:IElementHandle) -> d.EvaluateFunctionAsync<string> $"node => node.getAttribute('{name}')" |> runSync
let getAttributes = fun (d:IElementHandle) ->
    let attributeNames = getAttributeNames d
    attributeNames
    |> List.ofArray
    |> List.map (fun x -> x, getAttributeValue x d)
    |> Map.ofList

let regexExtract  regex                      text = Regex.Match(text, regex).Value
let regexExtractg regex                      text = Regex.Match(text, regex).Groups.[1].Value
let regexExtracts regex                      text = Regex.Matches(text, regex) |> Seq.map (fun x -> x.Value)
let regexReplace  regex (replacement:string) text = Regex.Replace(text, regex, replacement)
let regexRemove   regex                      text = Regex.Replace(text, regex, String.Empty)

// https://stackoverflow.com/a/61304202/2377787
let waitTillHTMLRendered (page: IPage) =
    let timeout = 30000
    let checkDurationMilliseconds = 1000
    let maxChecks = timeout / checkDurationMilliseconds
    let mutable lastHTMLSize = 0
    let mutable checkCounts = 1
    let mutable countStableSizeIterations = 0
    let minStableSizeIterations = 3

    printfn "waiting till HTML is fully rendered"
    while checkCounts <= maxChecks do
        checkCounts <- checkCounts + 1
        let html = page.GetContentAsync().Result
        let currentHTMLSize = html.Length

        printfn "lastHTMLSize ?= currentHTMLSize %d ?= %d" lastHTMLSize currentHTMLSize

        if lastHTMLSize <> 0 && currentHTMLSize = lastHTMLSize then
            countStableSizeIterations <- countStableSizeIterations + 1
        else
            countStableSizeIterations <- 0 //reset the counter

        if countStableSizeIterations >= minStableSizeIterations then
            printfn "page fully rendered"
            checkCounts <- maxChecks+1

        lastHTMLSize <- currentHTMLSize
        Thread.Sleep checkDurationMilliseconds

let env name = fun _ -> Environment.GetEnvironmentVariable(name)

let downloadDefaultBrowser () =
    let fetcher = new BrowserFetcher()

    let isDefaultBrowserAvailable =
        fetcher.GetInstalledBrowsers()
        |> Seq.length > 0

    if isDefaultBrowserAvailable |> not then
        fetcher.DownloadAsync() |> wait

let getPage args =
    let opt = new LaunchOptions(Headless = false, DefaultViewport = ViewPortOptions(), Args = args)
    let brw = Puppeteer.LaunchAsync opt |> runSync
    brw.PagesAsync() |> runSync |> Array.exactlyOne

let dumpSnapshot (page: IPage) =
    let timestamp = DateTime.Now.ToString("O").Replace(":", "_")
    Directory.CreateDirectory "snapshots" |> ignore
    let path = $"snapshots/page_{timestamp}.mhtml"

    let client = page.CreateCDPSessionAsync() |> runSync
    // https://chromedevtools.github.io/devtools-protocol/tot/Page/#method-captureSnapshot
    let raw = client.SendAsync("Page.captureSnapshot", {| format = "mhtml" |}) |> runSync
    let content = raw.Value.GetProperty "data" |> string
    File.WriteAllText(path, content, Encoding.UTF8)
    printfn "dumped snapshot to %s" path

let assertNotNull x =
    if isNull x then
        failwith "assertNotNull failed"
