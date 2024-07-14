namespace clients

open System
open System.Threading
open System.Threading.Tasks
open PuppeteerSharp

module Utils =
    let sleep x = x |> TimeSpan.FromSeconds |> Thread.Sleep

    let waitForSelectorAndClick (p:IPage) xpath =
        // since we want to click the element we wait for it to be accessible and visible
        // you can't click an invisible (but accessible element)
        let opt = WaitForSelectorOptions()
        opt.Visible <- true
        opt.Timeout <- 2 * 60 * 1000 // 2 minutes
        let s = p.WaitForSelectorAsync(xpath, opt).Result
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
