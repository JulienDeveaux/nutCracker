$(document).ready(() => 
{
    let btn = $("#send");
    
    btn.click(() =>
    {
        btn.attr("disabled", true);
        let hash = $("#hash").val();
        
        let onResponded = (data) =>
        {
            if(data.error)
            {
                $("#response").text('error: ' + data.error);
            }
            else if(data.mdp)
            {
                $("#response").text('résultat: ' + data.mdp);
            }

            btn.attr("disabled", false);
        };
        
        $.post("/", { hash: hash }, onResponded).fail((error) =>
        {
            console.error(error)
            onResponded({error: error.responseJSON.error});
        });
    });
});