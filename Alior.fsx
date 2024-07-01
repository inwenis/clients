#r "nuget: FSharp.Data, 6.4.0"
#r "nuget: PuppeteerSharp, 18.0.2"

#load "Types.fs"
#load "Utils.fs"
#load "Alior.fs"

open clients.Alior

let ac = AliorClient()
ac.SignIn()
// ac.Scrape()


open clients.Utils
open System.IO
open System

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

// stopped here trying to get list of products

let products = p.QuerySelectorAllAsync("xpath///*[contains(@id,'option_product')]") |> Async.AwaitTask |> Async.RunSynchronously

let x = products.[0].QuerySelectorAsync("xpath///@id") |> Async.AwaitTask |> Async.RunSynchronously
x.GetPropertyAsync("value")|> Async.AwaitTask |> Async.RunSynchronously

products
|> List.ofArray
|> List.map ()
let products =
    [
        """xpath///*[@id="option_product_***REMOVED***"]"""
        """xpath///*[@id="option_product_***REMOVED***"]"""
        """xpath///*[@id="option_product_***REMOVED***"]"""
        """xpath///*[@id="option_product_***REMOVED***"]"""
        """xpath///*[@id="option_product_***REMOVED***"]"""
        """xpath///*[@id="option_product_***REMOVED***"]"""
        """xpath///*[@id="option_product_***REMOVED***"]"""
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
