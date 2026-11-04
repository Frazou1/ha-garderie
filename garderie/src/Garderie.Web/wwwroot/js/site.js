// Garderie - comportements de page sans script en ligne (CSP) / page behaviours without inline script (CSP)
(function () {
    "use strict";

    // Confirmation avant suppression : <form class="confirmer" data-confirmer="Question ?">
    // Confirmation before delete
    document.addEventListener("submit", function (e) {
        var f = e.target;
        if (f instanceof HTMLFormElement && f.classList.contains("confirmer")) {
            if (!window.confirm(f.getAttribute("data-confirmer") || "Confirmer ?")) {
                e.preventDefault();
            }
        }
    });

    // Presences : un bouton choisi vide la liste « Autre », et inversement ; marque la case modifiee
    // Attendance: picking a button clears the "Other" list and vice versa; marks the cell as changed
    document.addEventListener("change", function (e) {
        var el = e.target;
        var cellule = el.closest ? el.closest("fieldset.cellule") : null;
        if (!cellule) return;
        if (el.matches("input[type=radio]")) {
            var autre = cellule.querySelector("select.autre");
            if (autre) autre.value = "";
        } else if (el.matches("select.autre") && el.value !== "") {
            cellule.querySelectorAll("input[type=radio]").forEach(function (r) { r.checked = false; });
        }
        cellule.classList.remove("propose");
        cellule.classList.add("modifie");
    });
})();
