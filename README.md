# TypeGap

TypeGap is a library designed to bring your frontend and backend code closer together by providing an api to emit typescript definitions from ASP.Net projects.

It's not tied to any specific generation method, feel free to generate typescript via T4, a console project, [custom msbuild task](https://github.com/caesay/BuildBud), etc. It currently supports the following:

- ASP.Net WebApi
- SignalR Hubs
- Static enums
- Everything that [TypeLite](http://type.litesolutions.net/) supports

It will recursively generate definitions for anything you provide, including sub-classes and all of the request / response models in the controllers / hubs.

```csharp
new TypeFluent()
    .AddWebApi<HomeController>()
    .AddWebApi<AccountController>()
    .Add<OtherModel>()
    .Add<MoreClasses>()
    .WithConstEnums(false)
    .WithGlobalNamespace("__generated")
    .Build(outputDir);
```

It will also generate helper code to execute the ajax calls that requires `moment` and `jquery`, feel free to copy this out of the generated services file and modify as desired to perform the ajax calls however you want. Then disable this from being emitted with the following:

```csharp
.WithAjaxHelper(false)
```

# Sample

It will emit regular models and enums as regular TypeScript interfaces. For the WebApi it will emit a class which is capable of executing all of the methods on your controller that might look similar to the following:

```js
export class AccountService {
    private hostname: string;
    public constructor(hostname: string) {
        this.hostname = (hostname.substr(-1) == "/") ? hostname : hostname + "/";
    }

    public AddExternalLogin(model: __generated.AddExternalLoginBindingModel, ajaxOptions?: IExtendedAjaxSettings): JQueryPromise<any> {
        var url = this.hostname + "api/Account/AddExternalLogin";
        return Ajax.post(url, model, ajaxOptions);
    }

    public ChangePassword(model: __generated.ChangePasswordBindingModel, ajaxOptions?: IExtendedAjaxSettings): JQueryPromise<any> {
        var url = this.hostname + "api/Account/ChangePassword";
        return Ajax.post(url, model, ajaxOptions);
    }

    public GetExternalLogin(provider: string, error?: string, ajaxOptions?: IExtendedAjaxSettings): JQueryPromise<any> {
        var url = this.hostname + "api/Account/ExternalLogin?provider=" + provider + "&error=" + error + "";
        return Ajax.get(url, null, ajaxOptions);
    }

    public GetExternalLogins(returnUrl: string, generateState?: boolean, ajaxOptions?: IExtendedAjaxSettings): JQueryPromise<__generated.ExternalLoginViewModel[]> {
        var url = this.hostname + "api/Account/ExternalLogins?returnUrl=" + returnUrl + "&generateState=" + generateState + "";
        return Ajax.get(url, null, ajaxOptions);
    }
}
```