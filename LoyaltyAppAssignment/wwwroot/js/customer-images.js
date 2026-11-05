(function (window, $) {
    "use strict";

    var apiBaseUrl = "https://alpacaapi.xcode.com.my/";

    function imageUrl(image, folder) {
        if (typeof image !== "string" || !image.trim()) {
            return null;
        }

        var path = image.trim().replace(/\\/g, "/");

        // Some API records contain a complete URL; uploaded images contain a filename.
        if (/^https?:\/\//i.test(path)) {
            return path;
        }

        // Do not treat another URI scheme as a filename.
        if (/^[a-z][a-z0-9+.-]*:/i.test(path) || path.startsWith("//")) {
            return null;
        }

        path = path.replace(/^~?\/+/, "").replace(/^wwwroot\//i, "");
        if (!path) {
            return null;
        }

        if (!path.includes("/")) {
            path = folder + "/" + path;
        }

        return apiBaseUrl + path.split("/").map(function (segment) {
            // Preserve already escaped filenames without encoding them twice.
            try {
                return encodeURIComponent(decodeURIComponent(segment));
            } catch (_) {
                return encodeURIComponent(segment);
            }
        }).join("/");
    }

    function create(image, folder, alt, className, fallback) {
        var container = $("<div>").addClass(className);
        var url = imageUrl(image, folder);

        function showFallback() {
            container.empty().addClass("api-image-fallback")
                .attr({ role: "img", "aria-label": "Image unavailable" })
                .append($("<span>").attr("aria-hidden", "true").text(fallback));
        }

        if (!url) {
            showFallback();
            return container;
        }

        var img = $("<img>").addClass("api-card-image")
            .attr({ alt: alt || "", loading: "lazy", decoding: "async" })
            .one("error", showFallback);

        img.attr("src", url);
        container.append(img);
        return container;
    }

    window.customerImages = { url: imageUrl, create: create };
})(window, jQuery);
