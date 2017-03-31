import * as moment from "moment";

const dateTimeRegex = /^(?:\d{2}){1,2}-\d{2}-(?:\d{2}){1,2}T\d{1,2}:\d{1,2}:\d{1,2}(\.\d+)?$/;

function translateAllDateTimes(obj: any) {
    for (var key in obj) {
        var val = obj[key];

        if (typeof val == "object") {
            val = translateAllDateTimes(val);
        } else if (typeof val == "string" && dateTimeRegex.test(val)) {
            val = moment(val).toDate();
        }

        obj[key] = val;
    }
    return obj;
}

export interface IExtendedAjaxSettings extends JQueryAjaxSettings {
    errorHandler?: (settings: IExtendedAjaxSettings, jqXhr: JQueryXHR) => void;
    preventDefaultErrorHandler?: boolean;
}

export function parseExceptionMessage(ex: any): string {
    if (!!ex.responseText) {
        ex = JSON.parse(ex.responseText);
    }

    var message: any;
    if (!!ex.ExceptionMessage) {
        message = ex.ExceptionMessage;
    } else if (!!ex.Message) {
        message = ex.Message;
    } else if (!!ex.promise) {
        message = "Unable to connect to the server.";
    } else {
        message = ex;
    }
    return message;
}

export function defaultErrorHandler(settings: IExtendedAjaxSettings, jqXhr: JQueryXHR) {
    if (settings.preventDefaultErrorHandler) return;
    var alertMethod = (<any>window).app ? (<any>window).app.showMessage : alert;
    var message = parseExceptionMessage(jqXhr);
    alertMethod(message);
}

export class Ajax {
    private _ajaxDefaults: IExtendedAjaxSettings = {
        cache: false,
        dataType: "json",
        timeout: 120000,
        crossDomain: false,
        errorHandler: defaultErrorHandler,
    }

    constructor(ajaxDefaults?: IExtendedAjaxSettings) {
        if (!!ajaxDefaults) {
            this._ajaxDefaults = $.extend({}, this._ajaxDefaults, ajaxDefaults);
        }
    }

    public post(url: string, data: any = null, ajaxOptions: IExtendedAjaxSettings = null): JQueryPromise<any> {
        //Apply custom ajaxsettings
        var settings: IExtendedAjaxSettings = $.extend({}, this._ajaxDefaults, ajaxOptions);
        settings.type = 'POST';
        // Allow data to be overridden by passing it in via ajaxOptions parameter.
        if (!settings.data) settings.data = data;

        // Workaround for arrays: http://aspnetwebstack.codeplex.com/workitem/177
        if (settings.data instanceof Array) {
            settings.data = { '': settings.data };
        }

        // If contentType is JSON, then serialize the data if not already.
        if (typeof settings.data !== "string" && settings.contentType === "application/json") {
            settings.data = JSON.stringify(settings.data);
        }

        return $.ajax(url, settings).fail((jqXhr: JQueryXHR) => {
            settings.errorHandler(settings, jqXhr);
        }).then(v => {
            return translateAllDateTimes(v);
        });
    }

    public get(url: string, data: any = null, ajaxOptions: IExtendedAjaxSettings = null) {
        //Apply custom ajaxsettings
        var settings: IExtendedAjaxSettings = $.extend({}, this._ajaxDefaults, ajaxOptions, { type: 'GET' });
        settings.type = 'GET';

        return $.ajax(url, settings).fail((jqXhr: JQueryXHR) => {
            settings.errorHandler(settings, jqXhr);
        }).then(v => {
            return translateAllDateTimes(v);
        });
    }
}