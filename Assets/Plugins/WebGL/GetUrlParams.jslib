mergeInto(LibraryManager.library, {
    GetURLParameter: function (paramNamePtr) {
        // Convert pointer to JS string
        var paramName = UTF8ToString(paramNamePtr);

        // Parse URL parameters
        var urlParams = new URLSearchParams(window.location.search);
        var value = urlParams.get(paramName);

        // Return empty string if not found
        if (value === null) value = "";

        // Allocate memory for the string and return pointer
        var buffer = _malloc(lengthBytesUTF8(value) + 1);
        stringToUTF8(value, buffer, lengthBytesUTF8(value) + 1);
        return buffer;
    }
});
