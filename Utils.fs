namespace clients

open System
open System.Threading
open System.Threading.Tasks
open PuppeteerSharp

module Utils =
    let sleep x = x |> TimeSpan.FromSeconds |> Thread.Sleep

    let waitForSelectorAndClick (p:IPage) xpath =
        p.WaitForSelectorAsync(xpath).Result.ClickAsync().Wait()

    let waitForSelectorAndType (p:IPage) xpath text =
        p.WaitForSelectorAsync(xpath).Result.TypeAsync(text).Wait()

    let wait (t:Task) = t.Wait()
    let runSync (t:Task<'a>) = t.Result

    let clickElement (e:IElementHandle) =
        e.ClickAsync() |> wait

    let clickSelector xpath (e:IElementHandle) =
        e.WaitForSelectorAsync(xpath).Result.ClickAsync().Wait()
