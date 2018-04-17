function _build_url(basePath: any, pathArray: any, queryArray: any): string {
    // turn url fragments into a full path, and validate user input
    let output = _trim_url(basePath);

    function encode(data, pname: string, isOptional: boolean, isRoute: boolean): string {
        if (!_is_real(data)) {
            if (isRoute || !isOptional)
                throw new Error("Parameter '" + pname + "' is not optional but a value was not provided.");
            return null;
        }
        data = encodeURIComponent(data);
        return isRoute ? data : (pname + "=" + data);
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