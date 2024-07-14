namespace clients

open System
open System.Threading
open System.Threading.Tasks
open PuppeteerSharp

module Utils =
    let sleep x = x |> TimeSpan.FromSeconds |> Thread.Sleep

    let waitForSelectorAndClick (p:IPage) xpath =
        let s = p.WaitForSelectorAsync(xpath).Result
        s.ClickAsync().Wait()

    let waitForSelectorAndType (page:IPage) xpath text =
        let e = page.WaitForSelectorAsync(xpath).Result
        e.TypeAsync(text).Wait()

    let wait (t:Task<'a>) = t.Wait()
    let wait2 (t:Task) = t.Wait()
    let run_sync (t:Task<'a>) = t.Result

    let clickElement (e:IElementHandle) =
        e.ClickAsync() |> wait2

    let clickSelector xpath (e:IElementHandle) =
        let x = e.WaitForSelectorAsync(xpath).Result
        x.ClickAsync().Wait()
