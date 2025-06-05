using Hx.Rx;

namespace Hx.Components.Error;

public static class ErrorHandler {
    public static IResult Get(HttpResponse response, ErrorModel model) {
        return response.RenderComponent<ErrorPage, ErrorModel>(model);
    }
}
