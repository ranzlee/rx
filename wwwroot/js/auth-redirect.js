var params = new URL(document.location.toString()).searchParams;
var path = params.get("ReturnUrl") ?? "/";
if (path.toString().trim() === "") {
    path = "/";
}
window.location.replace(path);