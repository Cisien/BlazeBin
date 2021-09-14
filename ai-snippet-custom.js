(function (win, doc, snipConfig) {
    var scriptText = "script";
    var strDisableExceptionTracking = "disableExceptionTracking";
    var strUndefined = "undefined";
    var strCrossOrigin = "crossOrigin";

    var sdkInstanceName = "appInsightsSDK";         // required for Initialization to find the current instance
    var aiName = snipConfig.name || "appInsights";  // provide non default instance name through snipConfig name value
    if (snipConfig.name || win[sdkInstanceName]) {
        // Only set if supplied or another name is defined to avoid polluting the global namespace
        win[sdkInstanceName] = aiName;
    }
    var aiSdk = win[aiName] || (function (aiConfig) {
        var appInsights = {
            initialize: true,   // initialize sdk on download
            queue: [],
            sv: "5",            // Track the actual snippet version for reporting.
            version: 2.0,       // initialization version, if this is not 2.0 the previous scripts fail to initialize
            config: aiConfig
        };

        // Assigning these to local variables allows them to be minified to save space:
        var targetSrc = aiConfig.url || snipConfig.src;
        if (targetSrc) {

            function _createScript() {
                var scriptElement = doc.createElement(scriptText);
                scriptElement.src = targetSrc;

                // Allocate Cross origin only if defined and available
                var crossOrigin = snipConfig[strCrossOrigin];
                if ((crossOrigin || crossOrigin === "") && scriptElement[strCrossOrigin] != strUndefined) {
                    scriptElement[strCrossOrigin] = crossOrigin;
                }

                return scriptElement;
            }

            var theScript = _createScript();
            var headNode = doc.getElementsByTagName("head")[0];
            headNode.appendChild(theScript);
        }

        // capture initial cookie
        try {
            appInsights.cookie = doc.cookie;
        } catch (e) { }

        function _createMethods(methods) {
            while (methods.length) {
                (function (name) {
                    // Define a temporary method that queues-up a the real method call
                    appInsights[name] = function () {
                        // Capture the original arguments passed to the method
                        var originalArguments = arguments;
                        appInsights.queue.push(function () {
                            // Invoke the real method with the captured original arguments
                            appInsights[name].apply(appInsights, originalArguments);
                        });
                    };
                })(methods.pop());
            }
        }

        var track = "track";
        var trackPage = "TrackPage";
        var trackEvent = "TrackEvent";
        _createMethods([track + "Event",
        track + "PageView",
        track + "Exception",
        track + "Trace",
        track + "DependencyData",
        track + "Metric",
        track + "PageViewPerformance",
        "start" + trackPage,
        "stop" + trackPage,
        "start" + trackEvent,
        "stop" + trackEvent,
            "addTelemetryInitializer",
            "setAuthenticatedUserContext",
            "clearAuthenticatedUserContext",
            "flush"]);

        // expose SeverityLevel enum
        appInsights['SeverityLevel'] = {
            Verbose: 0,
            Information: 1,
            Warning: 2,
            Error: 3,
            Critical: 4
        };

        // Collect global errors
        // Note: ApplicationInsightsAnalytics is the extension string identifier for
        //  AppAnalytics. It is defined in ApplicationInsights.ts:ApplicationInsights.identifer
        var analyticsCfg = ((aiConfig.extensionConfig || {}).ApplicationInsightsAnalytics || {});
        if (!(aiConfig[strDisableExceptionTracking] === true || analyticsCfg[strDisableExceptionTracking] === true)) {
            var method = "onerror";
            _createMethods(["_" + method]);
            var originalOnError = win[method];
            win[method] = function (message, url, lineNumber, columnNumber, error) {
                var handled = originalOnError && originalOnError(message, url, lineNumber, columnNumber, error);
                if (handled !== true) {
                    appInsights["_" + method]({
                        message: message,
                        url: url,
                        lineNumber: lineNumber,
                        columnNumber: columnNumber,
                        error: error,
                        evt: win.event
                    });
                }

                return handled;
            };
            aiConfig.autoExceptionInstrumented = true;
        }

        return appInsights;
    })(snipConfig.cfg);

    // global instance must be set in this order to mitigate issues in ie8 and lower
    win[aiName] = aiSdk;

    function _onInit() {
        if (snipConfig.onInit) {
            snipConfig.onInit(aiSdk);
        }
    }

    // if somebody calls the snippet twice, don't report page view again
    if (aiSdk.queue && aiSdk.queue.length === 0) {
        aiSdk.queue.push(_onInit);
        aiSdk.trackPageView({});
    } else {
        // Already loaded so just call the onInit
        _onInit();
    }
})(window, document, {
    src: "https://js.monitor.azure.com/scripts/b/ai.2.min.js", // The SDK URL Source
    // name: "appInsights", // Global SDK Instance name defaults to "appInsights" when not supplied
    // ld: 0, // Defines the load delay (in ms) before attempting to load the sdk. -1 = block page load and add to head. (default) = 0ms load after timeout,
    // useXhr: 1, // Use XHR instead of fetch to report failures (if available),
    // crossOrigin: "anonymous", // When supplied this will add the provided value as the cross origin attribute on the script tag
    // onInit: null, // Once the application insights instance has loaded and initialized this callback function will be called with 1 argument -- the sdk instance (DO NOT ADD anything to the sdk.queue -- As they won't get called)
    cfg: { // Application Insights Configuration
        instrumentationKey: "INSTRUMENTATION_KEY"
    }
});