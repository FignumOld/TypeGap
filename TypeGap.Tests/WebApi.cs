using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Web.Http;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace TypeGap.Tests
{
    [TestClass]
    public class WebApi
    {
        [TestMethod]
        public void TestSimpleController()
        {
            var builder = new TypeFluent();

            builder.AddApiObject(typeof(SimpleController));

            //var simple = builder.AddApiDescription("SimpleController");

            //simple.AddAction("RunTestRoute", typeof(void), ApiMethod.Get, "test/route/{key}", new[]
            //{
            //    new ApiParamDesc()
            //    {
            //        ParameterName = "key",
            //        IsOptional = false,
            //        ParameterType = typeof(string),
            //    },
            //    new ApiParamDesc()
            //    {
            //        ParameterName = "value",
            //        IsOptional = false,
            //        ParameterType = typeof(int),
            //    },
            //});
            //simple.AddAction("TestGet", typeof(SimpleModel));
            //simple.AddAction("TestPost", typeof(SimpleModel), ApiMethod.Post);

            var output = builder.Build();

            var check = @"import { Ajax, IExtendedAjaxSettings } from ""./Ajax"";

export class Services {
    public readonly Simple: SimpleService;
    public constructor(hostname: string, ajaxDefaults?: IExtendedAjaxSettings) {
        this.Simple = new SimpleService(hostname, ajaxDefaults);
    }
}

export class SimpleService {
    private _hostname: string;
    private _ajax: Ajax;
    public constructor(hostname: string, ajaxDefaults?: IExtendedAjaxSettings) {
        this._hostname = (hostname.substr(-1) == ""/"") ? hostname : hostname + ""/"";
        this._ajax = new Ajax(ajaxDefaults);
    }
    
    public RunTestRoute(key: string, value: number, ajaxOptions?: IExtendedAjaxSettings): JQueryPromise<any> {
        var url = this._hostname + ""api/Simple/test/route/"" + key + ""?value="" + value + """";
        return this._ajax.get(url, null, ajaxOptions);
    }
    
    public TestGet(ajaxOptions?: IExtendedAjaxSettings): JQueryPromise<TypeGap.Tests.SimpleModel> {
        var url = this._hostname + ""api/Simple/TestGet"";
        return this._ajax.get(url, null, ajaxOptions);
    }
    
    public TestPost(ajaxOptions?: IExtendedAjaxSettings): JQueryPromise<TypeGap.Tests.SimpleModel> {
        var url = this._hostname + ""api/Simple/TestPost"";
        return this._ajax.post(url, null, ajaxOptions);
    }
}

interface ISignalRPromise<T> {
    done(cb: (result: T) => any): ISignalRPromise<T>;
    error(cb: (error: any) => any): ISignalRPromise<T>;
}

interface SignalR {
}


";
            //Assert.AreEqual(check.Trim(), output.ServicesTS.Trim());
        }

        [TestMethod]
        public void TestSimpleModel()
        {
//            var output = new TypeFluent()
//                .Add<SimpleModel>()
//                .WithAjaxHelper(false)
//                .Build();

//            var check = @"
//declare namespace TypeGap.Tests {
//    interface SimpleModel {
//        ADateTime: Date;
//        ADecimal: number;
//        ADictionary: { [key: string]: string };
//        AString: string;
//    }
//}
//";
//            Assert.AreEqual(check.Trim(), output.DefinitionTS.Trim());
        }
    }


    public class SimpleController : ApiController
    {
        public string Test { get; set; }

        [HttpPost]
        public Task<SimpleModel> TestPost()
        {
            throw new NotImplementedException();
        }

        [HttpGet]
        public Task<SimpleModel> TestGet()
        {
            throw new NotImplementedException();
        }

        [HttpGet, Route("test/route/{key}")]
        public Task RunTestRoute(string key, int value)
        {
            throw new NotImplementedException();
        }
    }

    public class SimpleModel
    {
        public string AString { get; set; }
        public DateTime ADateTime { get; set; }
        public Dictionary<string, string> ADictionary { get; set; }
        public decimal ADecimal { get; set; }
    }
}
