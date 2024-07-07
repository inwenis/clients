#r "nuget: FSharp.Data, 6.4.0"
#r "nuget: PuppeteerSharp, 18.0.2"

#load "Utils.fs"
#load "Alior.fs"

open System.IO
open System
open PuppeteerSharp

open clients.Utils
open clients.Alior


let username () = System.Environment.GetEnvironmentVariable("ALIOR_USERNAME")
let password () = System.Environment.GetEnvironmentVariable("ALIOR_PASSWORD")

let ac = AliorClient(username, password)
ac.SignIn()

let p = ac.GetP()
// go to Dashboard (aka. home page) first, if you're already on "Payments page" you can't click "New payment"
waitForSelectorAndClick p "xpath///*[contains(text(),'Dashboard')]"
sleep 2
waitForSelectorAndClick p "xpath///*[contains(text(),'Payments')]"
sleep 2 // need to sleep otherwise the New Payment won't work
waitForSelectorAndClick p "xpath///*[contains(text(),'Payment history')]"
sleep 2
waitForSelectorAndClick p "xpath///*[contains(text(),'Show filters')]"
sleep 2
// click Period
waitForSelectorAndClick p """xpath///*[@id="app-content"]/div[2]/div/payments/div/payment-history/section/div/div/div/history/div/history-header/div/form/div/div/div[2]/history-filters/div/div[1]/div/div/div[3]/history-filter-time/fieldset/div/custom-select/div/div/div/div[1]"""
// todo - make this a moving range, picking just last year - see if we lose transaction on year change here
waitForSelectorAndClick p """xpath///*[@id="option_time_LAST_YEAR"]"""

waitForSelectorAndClick p """xpath///*[@id="app-content"]/div[2]/div/payments/div/payment-history/section/div/div/div/history/div/history-header/div/form/div/div/div[2]/history-filters/div/div[1]/div/div/div[8]/history-export/div/div/div[2]/custom-select/div/div/div/div[1]"""
sleep 2
// click csv
waitForSelectorAndClick p """xpath///*[@id="option_document_type_CSV"]"""
sleep 2
let x = p.GetContentAsync() |> Async.AwaitTask |> Async.RunSynchronously
File.WriteAllText("out.html", x)
// stopped here trying to get list of products

let products = p.QuerySelectorAllAsync("xpath///*[contains(@id,'option_product')]") |> Async.AwaitTask |> Async.RunSynchronously

p.GoToAsync("https://www.wikipedia.org/").Result

let ds = p.QuerySelectorAllAsync("xpath///div").Result
let d = ds.[0]
d.GetPropertiesAsync().Result.Keys.Count

let l_options = new LaunchOptions(Headless = false, DefaultViewport = ViewPortOptions())
let b = Puppeteer.LaunchAsync(l_options) |> run_sync
p.GoToAsync("file://c:/git/clients/out.html").Result

d.GetPropertiesAsync
d.GetPropertyAsync("innerText").Result
d.EvaluateFunctionAsync<string>("node => node.getAttribute('class')").Result

d.EvaluateFunctionAsync<string[]>("node => Array.from(node.attributes).map(x => x.name)").Result

let x = d.GetPropertyAsync("class").Result

ds
|> List.ofArray
|> List.map (fun d -> d.EvaluateFunctionAsync<string[]>("node => Array.from(node.attributes).map(x => x.name)").Result)


let x = products.[1].GetPropertyAsync("value") |> Async.AwaitTask |> Async.RunSynchronously
products.[1].JsonValueAsync().Result
products.[1].GetPropertiesAsync() |> Async.AwaitTask |> Async.RunSynchronously

x.RemoteObject.Value.ToString()
x.GetPropertyAsync("value")|> Async.AwaitTask |> Async.RunSynchronously

products
|> List.ofArray
|> List.map ()
let products =
    [
        """xpath///*[@id="option_product_TODO"]"""
        """xpath///*[@id="option_product_TODO"]"""
        """xpath///*[@id="option_product_TODO"]"""
        """xpath///*[@id="option_product_TODO"]"""
        """xpath///*[@id="option_product_TODO"]"""
        """xpath///*[@id="option_product_TODO"]"""
        """xpath///*[@id="option_product_TODO"]"""
    ]

// transactions must be downloaded per product separately. If all products are selected internal transaction are messed up.
for product_selector in products do
    // click Product
    waitForSelectorAndClick p """xpath///*[@id="app-content"]/div[2]/div/payments/div/payment-history/section/div/div/div/history/div/history-header/div/form/div/div/div[2]/history-filters/div/div[1]/div/div/div[6]/history-filter-product/fieldset/div/custom-select/div/span/div/div[1]"""
    sleep 2

    waitForSelectorAndClick p product_selector
    sleep 2
    p.Keyboard.PressAsync("Escape").Wait() // close Product drop-down
    sleep 2

    // Apply filters
    waitForSelectorAndClick p """xpath///*[@id="app-content"]/div[2]/div/payments/div/payment-history/section/div/div/div/history/div/history-header/div/form/div/div/div[2]/history-filters/div/div[1]/div/div/div[9]/button-cta/button"""
    sleep 2

    // click Download
    waitForSelectorAndClick p """xpath///*[@id="app-content"]/div[2]/div/payments/div/payment-history/section/div/div/div/history/div/history-header/div/form/div/div/div[2]/history-filters/div/div[1]/div/div/div[8]/history-export/div/div/div[3]/button-cta/button/span"""
    sleep 5
    printfn "File should be ready in \"Downloads\" folder"

    // deselect product
    // click Product
    waitForSelectorAndClick p """xpath///*[@id="app-content"]/div[2]/div/payments/div/payment-history/section/div/div/div/history/div/history-header/div/form/div/div/div[2]/history-filters/div/div[1]/div/div/div[6]/history-filter-product/fieldset/div/custom-select/div/span/div/div[1]"""
    sleep 2
    waitForSelectorAndClick p product_selector
    sleep 2
    p.Keyboard.PressAsync("Escape").Wait() // close Product drop-down
    sleep 2

    let home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
    let file =
        Directory.EnumerateFiles(Path.Combine(home,"Downloads"), "*.csv")
        |> List.ofSeq
        |> List.map              (fun x -> new FileInfo(x))
        |> List.filter           (fun x -> DateTimeOffset.UtcNow - DateTimeOffset(x.CreationTimeUtc) < TimeSpan.FromSeconds(60.))
        |> List.sortByDescending (fun x -> x.CreationTimeUtc)
        |> List.tryHead

    match file with
    | Some f ->
        printfn "found file, moving to 'finances'"
        File.Copy(f.FullName, Path.Combine("./input_data/alior/", f.Name), true)
    | None -> printfn "not found"

// todo - fix paths in scraping
// todo make default timeout in waitForSelectorAndClick smaller
// todo remove product ids hardcoded from here
//  remove them from git history
// todo make login/password passed via variables
