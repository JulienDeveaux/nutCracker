$(document).ready(() => 
{
    $("#send").click(() =>
    {
        let hash = $("#hash").val();
        
        $.post("/", { hash: hash }, (data) => 
        {
            $("#response").text(data);
        });
    });
});