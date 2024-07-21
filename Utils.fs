namespace clients

open System
open System.Threading
open System.Threading.Tasks
open PuppeteerSharp

module Utils =
    let sleep x = x |> TimeSpan.FromSeconds |> Thread.Sleep

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
        options.Delay <- TimeSpan.FromSeconds(1).TotalMilliseconds |> int
        p.WaitForSelectorAsync(xpath) |> runSync |> fun x -> x.TypeAsync(text, options) |> wait
