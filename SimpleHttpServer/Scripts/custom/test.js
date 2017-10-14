$("document").ready(function () {
    $(".button").on("click",
        function() {
            $(this).toggleClass("is-danger");
        });
});