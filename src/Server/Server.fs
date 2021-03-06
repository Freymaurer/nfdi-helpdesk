module Server

open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Saturn
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Giraffe
open Shared

open Targets

let CaptchaStore = CaptchaStore.Storage()

open System.IO

let api (ctx: HttpContext) =
    {
        submitIssue = fun (formModel,captcha) -> async {
            let storedCaptcha = CaptchaStore.GetCaptcha(captcha.Id)
            let hasValidToken = captcha.AccessToken = storedCaptcha.Accesstoken
            if not hasValidToken then
                failwith "Error. Captcha access token is no longer valid. Please redo the captcha and try again."
            if formModel.IssueTopic.IsNone then failwith "Error. Could not find associated topic for issue."
            if formModel.IssueTitle = "" then failwith "Error. Cannot submit issue with empty title"
            try
                MSInterop.createPlannerTaskInTeams(formModel,ctx).Wait()
                CaptchaStore.RemoveCaptcha(storedCaptcha) |> ignore
            with
                | exn -> failwith $"Hit exception: {exn}"
            return ()
        }
        getCaptcha = fun () -> async {
            let newCaptcha = CaptchaStore.GenerateCaptcha()
            return newCaptcha
        }
        checkCaptcha = fun clientCaptcha -> async {
            let storedCaptcha = CaptchaStore.GetCaptcha(clientCaptcha.Id)
            let isCorrect = storedCaptcha.Cleartext = clientCaptcha.UserInput.Trim()
            let result =
                if isCorrect then 
                    Ok {clientCaptcha with AccessToken = storedCaptcha.Accesstoken}
                else
                    let wasRemoved = CaptchaStore.RemoveCaptcha(storedCaptcha)
                    let newCaptcha = CaptchaStore.GenerateCaptcha()
                    Error newCaptcha
            return result
        }
    }

let errorHandler (ex:exn) (routeInfo:RouteInfo<HttpContext>) =
    let msg = sprintf "%A %s @%s." ex.Message System.Environment.NewLine routeInfo.path
    Propagate msg


let webApp =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromContext api
    |> Remoting.withErrorHandler errorHandler
    |> Remoting.buildHttpHandler

let app =
    application {
        url "http://0.0.0.0:8085"
        use_router (webApp)
        memory_cache
        use_static "public"
        use_gzip
    }

app
    .ConfigureAppConfiguration(
        System.Action<Microsoft.Extensions.Hosting.HostBuilderContext,IConfigurationBuilder> ( fun ctx config ->
            config.AddJsonFile("helpdesk_config.json",true,true)            |> ignore
            config.AddUserSecrets("de50dd48-e691-4599-89ab-9d56efdaaafc")   |> ignore
        )
)
|> run