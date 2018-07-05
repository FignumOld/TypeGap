function _build_url(basePath: any, pathArray: any, queryArray: any[]): string {
    // turn url fragments into a full path, and validate user input
    let output = _trim_url(basePath);

    function encode(data: any, pname: string, isOptional: boolean, isRoute: boolean): string {
        if (!_is_real(data)) {
            if (isRoute || !isOptional)
                throw new Error("Parameter '" + pname + "' is not optional but a value was not provided.");
            return null;
        }
        if (isRoute) {
            return data;
        }
        if (Array.isArray(data)) {
            return data
                .map((item, i) => _serialize_data(item, pname + "[" + i + "]."))
                .join("&");
        }
        if (typeof data === "object") {
            return _serialize_data(data, pname + ".");
        }
        data = encodeURIComponent(data);
        return pname + "=" + data;
    }

    let skippedName: string;

    for (const data of pathArray) {
        if (typeof data === "string" || data instanceof String) {
            if (!!skippedName)
                throw new Error("Url literal '" + data + "' can't come after missing optional parameter '" + skippedName + "'");
            output += "/" + _trim_url(data);
            continue;
        }
        const paramName = data[1];
        const value = encode(data[0], paramName, false, true);
        if (!value) {
            skippedName = paramName;
            continue;
        } else if (!!skippedName) {
            throw new Error("A value was given for parameter '" + paramName + "', but the previous optional parameter '" + skippedName + "' was not provided.");
        } else {
            output += "/" + value;
        }
    }

    const queryParams = queryArray.map(data => encode(data[0], data[1], data[2], false));
    if (queryParams.length > 0) {
        output += "?" + queryParams.filter(x => !!x).join("&");
    }

    return output;
}

function _ensure_string(part: any) {
    if (!(typeof part === "string" || part instanceof String))
        throw new Error("Building path string, expected 'string' - recieved: " + part);
}

function _trim_url(part: any): string {
    // remove leading and trailing path separators from string
    _ensure_string(part);
    part = part.trim();
    part = (part.substr(-1) == "/") ? part.substr(0, part.length - 1) : part;
    part = (part.substr(0, 1) == "/") ? part.substr(1) : part;
    return part;
}

function _is_real(value: any): boolean {
    // check if a value is real or empty. blank strings considered empty, but zero is not.
    return value !== "" && value !== undefined && value !== null && !(Array.isArray(value) && value.length === 0);
}

function _serialize_data(obj: any, prefix: string): string {
    // serialize complex objects into key-value pairs with optional prefix
    const items = [];
    for (const p in obj) {
        if (obj.hasOwnProperty(p)) {
            var key = prefix ? prefix + p : p;
            var value = obj[p];
            const item = (value !== null && typeof value === "object") ? _serialize_data(value, key) : key + "=" + encodeURIComponent(value);
            items.push(item);
        }
    }
    return items.join("&");
}