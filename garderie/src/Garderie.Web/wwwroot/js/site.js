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

    // Classe de couleur d'un code de presence (meme regle que Affichage.ClasseCode)
    // Colour class of an attendance code (same rule as Affichage.ClasseCode)
    function classeCode(v) {
        if (v === "") return "code-vide";
        if (v === "P" || v === "P 1/2") return "code-p";
        if (v === "A" || v === "A 1/2") return "code-a";
        if (v === "F" || v === "F 1/2") return "code-f";
        if (v === "R" || v === "R 1/2") return "code-r";
        return "code-autre";
    }

    document.addEventListener("change", function (e) {
        var el = e.target;

        // Presences : la case change de couleur et devient « modifiee » / Attendance cell recoloured, marked as changed
        if (el instanceof HTMLSelectElement && el.classList.contains("code")) {
            ["code-vide", "code-p", "code-a", "code-f", "code-r", "code-autre", "propose", "enregistre"].forEach(function (c) {
                el.classList.remove(c);
            });
            el.classList.add(classeCode(el.value), "modifie");
            return;
        }

        // Photo : reduite a 640 px dans le navigateur (JPEG, sans donnees EXIF/GPS) puis envoyee tout de suite
        // Photo: resized to 640 px in the browser (JPEG, no EXIF/GPS) then sent right away
        if (el instanceof HTMLInputElement && el.type === "file" && el.classList.contains("photo-auto") && el.files && el.files[0]) {
            var form = el.form;
            reduire(el.files[0], 640).then(function (blob) {
                if (blob) {
                    var dt = new DataTransfer();
                    dt.items.add(new File([blob], "photo.jpg", { type: "image/jpeg" }));
                    el.files = dt.files;
                }
                form.classList.add("envoi");
                form.submit();
            });
        }
    });

    function reduire(fichier, max) {
        if (!window.createImageBitmap) return Promise.resolve(null);
        return createImageBitmap(fichier).then(function (img) {
            var r = Math.min(1, max / Math.max(img.width, img.height));
            var c = document.createElement("canvas");
            c.width = Math.round(img.width * r);
            c.height = Math.round(img.height * r);
            c.getContext("2d").drawImage(img, 0, 0, c.width, c.height);
            return new Promise(function (ok) { c.toBlob(ok, "image/jpeg", 0.85); });
        }).catch(function () { return null; }); // format non lu (ex. HEIC hors Safari) : envoi tel quel / sent as is
    }
})();
