namespace clients

open System
open System.Threading
open System.Threading.Tasks
open PuppeteerSharp
open System.Text.RegularExpressions

module Utils =
    let sleep x = x |> int64 |> TimeSpan.FromSeconds |> Thread.Sleep

    let wait (t:Task) = t.Wait()

    let runSync (t:Task<'a>) = t.Result

    let clickElement (e:IElementHandle) = e.ClickAsync() |> wait

    let clickSelector xpath (e:IElementHandle) =
        e.WaitForSelectorAsync(xpath) |> runSync |> clickElement

    let click (p:IPage) xpath = p.WaitForSelectorAsync(xpath) |> runSync |> clickElement

    let typet (p:IPage) xpath text =
        p.WaitForSelectorAsync(xpath) |> runSync |> fun x -> x.TypeAsync(text) |> wait

    let typeSlow (p:IPage) xpath text =
        let options = new PuppeteerSharp.Input.TypeOptions()
        options.Delay <- TimeSpan.FromSeconds(seconds=1).TotalMilliseconds |> int
        p.WaitForSelectorAsync(xpath) |> runSync |> fun x -> x.TypeAsync(text, options) |> wait

    let getAttributeNames = fun (d:IElementHandle) -> d.EvaluateFunctionAsync<string[]>("node => Array.from(node.attributes).map(x => x.name)") |> runSync
    let getAttributeValue = fun name (d:IElementHandle) -> d.EvaluateFunctionAsync<string>($"node => node.getAttribute('{name}')") |> runSync
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
    let waitTillHTMLRendered (page:IPage) =
        let timeout = 30000
        let checkDurationMilliseconds = 1000
        let maxChecks = timeout / checkDurationMilliseconds
        let mutable lastHTMLSize = 0
        let mutable checkCounts = 1
        let mutable countStableSizeIterations = 0
        let minStableSizeIterations = 3

        while checkCounts <= maxChecks do
            checkCounts <- checkCounts + 1
            let html = page.GetContentAsync().Result
            let currentHTMLSize = html.Length

            // not sure why this is here
            let bodyHTMLSize = page.EvaluateExpressionAsync("() => document.body.innerHTML.length").Result

            printfn "last: %A <> curr: %A body html size: %A" lastHTMLSize currentHTMLSize bodyHTMLSize

            if lastHTMLSize <> 0 && currentHTMLSize = lastHTMLSize then
                countStableSizeIterations <- countStableSizeIterations + 1
            else
                countStableSizeIterations <- 0 //reset the counter

            if countStableSizeIterations >= minStableSizeIterations then
                printfn "Page rendered fully.."
                checkCounts <- maxChecks+1

            lastHTMLSize <- currentHTMLSize
            sleep (checkDurationMilliseconds/1000)
