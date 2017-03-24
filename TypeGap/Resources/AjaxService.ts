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
    /**
     * Allows the default error handling to be suppressed.
     */
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
    } else {
        message = ex;
    }
    return message;
}

export class Ajax {

    private static ajaxDefaults: JQueryAjaxSettings = {
        cache: false,
        dataType: "json",
        timeout: 120000,
        crossDomain: false
    }

    public static post(url: string, data: any = null, ajaxOptions: IExtendedAjaxSettings = null): JQueryPromise<any> {
        //Apply custom ajaxsettings
        var settings = $.extend({}, this.ajaxDefaults, ajaxOptions);
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
            this.defaultErrorHandler(settings, jqXhr);
        }).then(v => {
            return translateAllDateTimes(v);
        });
    }

    public static get(url: string, data: any = null, ajaxOptions: IExtendedAjaxSettings = null) {
        //Apply custom ajaxsettings
        var settings = $.extend({}, this.ajaxDefaults, ajaxOptions, { type: 'GET' });
        settings.type = 'GET';

        return $.ajax(url, settings).fail((jqXhr: JQueryXHR) => {
            this.defaultErrorHandler(settings, jqXhr);
        }).then(v => {
            return translateAllDateTimes(v);
        });
    }

    private static defaultErrorHandler(settings: IExtendedAjaxSettings, jqXhr: JQueryXHR): JQueryPromise<any> {
        if (settings.preventDefaultErrorHandler) return;

        // Use durandal showMessage function if it's available, otherwise fallback to alert.
        var alertMethod = (<any>window).app ? (<any>window).app.showMessage : alert;

        var message = parseExceptionMessage(jqXhr);
        alertMethod(message);
    }
}