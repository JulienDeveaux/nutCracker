// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

function loadingFinish(mdp)
{
    document.getElementById("walnut-close").classList.add('hidden');
    document.getElementById("walnut-open").classList.remove('hidden');
    
    document.getElementById("walnut-message").textContent = "Résultat: " + mdp;
}
