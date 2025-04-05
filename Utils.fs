namespace clients

open System
open System.Threading
open System.Threading.Tasks
open PuppeteerSharp
open System.Text.RegularExpressions

module Utils =
    let sleep x = x |> fun y -> TimeSpan.FromSeconds(seconds=y) |> Thread.Sleep

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

    let extract regex text = Regex.Match(text, regex).Groups.[0].Value

    let getAttributeNames = fun (d:IElementHandle) -> d.EvaluateFunctionAsync<string[]>("node => Array.from(node.attributes).map(x => x.name)") |> runSync
    let getAttributeValue = fun name (d:IElementHandle) -> d.EvaluateFunctionAsync<string>($"node => node.getAttribute('{name}')") |> runSync
    let getAttributes = fun (d:IElementHandle) ->
        let attributeNames = getAttributeNames d
        attributeNames
        |> List.ofArray
        |> List.map (fun x -> x, getAttributeValue x d)
        |> Map.ofList
