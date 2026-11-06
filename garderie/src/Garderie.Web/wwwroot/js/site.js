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

    // Signature au doigt / a la souris : <canvas class="pad-signature"> + <input class="valeur-signature">
    // Finger / mouse signature
    document.querySelectorAll("canvas.pad-signature").forEach(function (c) {
        var g = c.getContext("2d");
        var trace = false, vide = true, dernier = null;
        g.lineWidth = 3.2; g.lineCap = "round"; g.lineJoin = "round"; g.strokeStyle = "#1d1b2b";
        function point(e) {
            var r = c.getBoundingClientRect();
            return { x: (e.clientX - r.left) * c.width / r.width, y: (e.clientY - r.top) * c.height / r.height };
        }
        c.addEventListener("pointerdown", function (e) {
            trace = true; dernier = point(e); c.setPointerCapture(e.pointerId);
            g.beginPath(); g.arc(dernier.x, dernier.y, 1.4, 0, 7); g.fillStyle = g.strokeStyle; g.fill();
            vide = false; e.preventDefault();
        });
        c.addEventListener("pointermove", function (e) {
            if (!trace) return;
            var p = point(e);
            g.beginPath(); g.moveTo(dernier.x, dernier.y); g.lineTo(p.x, p.y); g.stroke();
            dernier = p; e.preventDefault();
        });
        ["pointerup", "pointercancel", "pointerleave"].forEach(function (t) { c.addEventListener(t, function () { trace = false; }); });

        var form = c.closest("form");
        var bouton = form.querySelector(".effacer-signature");
        var methode = form.querySelector(".methode-signature");
        if (bouton) bouton.addEventListener("click", function () {
            g.clearRect(0, 0, c.width, c.height); vide = true;
            if (methode) methode.value = "dessin";
        });
        // Photo de la signature : fond retire, trait fonce garde / Signature photo: background removed, dark ink kept
        var importer = form.querySelector(".importer-signature");
        if (importer) importer.addEventListener("change", function () {
            var f = importer.files && importer.files[0];
            importer.value = "";
            if (!f) return;
            if (!window.createImageBitmap) { window.alert("Ce navigateur ne peut pas lire l'image."); return; }
            createImageBitmap(f).then(function (img) {
                var encre = extraireEncre(img);
                if (!encre) { window.alert("Aucune signature trouvée dans l'image : prenez la photo d'une signature foncée sur papier blanc."); return; }
                g.clearRect(0, 0, c.width, c.height);
                var k = Math.min((c.width - 20) / encre.width, (c.height - 20) / encre.height);
                var w = encre.width * k, h = encre.height * k;
                g.drawImage(encre, (c.width - w) / 2, (c.height - h) / 2, w, h);
                vide = false;
                if (methode) methode.value = "image";
            }).catch(function () { window.alert("Ce format d'image n'est pas lu ici. Essayez une photo JPG ou PNG."); });
        });
        form.addEventListener("submit", function (e) {
            if (vide) {
                e.preventDefault();
                window.alert("Signez dans le cadre avant d'envoyer.");
                return;
            }
            form.querySelector(".valeur-signature").value = recadrer(c).toDataURL("image/png");
            // Un seul envoi (double toucher) / Single submission (double tap)
            form.querySelectorAll("button[type=submit]").forEach(function (b) { b.disabled = true; });
            form.classList.add("envoi");
        });
    });

    // Garde les pixels fonces (encre) en transparence, recadres / Keeps dark pixels as ink, cropped
    function extraireEncre(img) {
        var r = Math.min(1, 1200 / Math.max(img.width, img.height));
        var t = document.createElement("canvas");
        t.width = Math.max(1, Math.round(img.width * r)); t.height = Math.max(1, Math.round(img.height * r));
        var g = t.getContext("2d");
        g.drawImage(img, 0, 0, t.width, t.height);
        var im = g.getImageData(0, 0, t.width, t.height), d = im.data, encre = 0;
        for (var i = 0; i < d.length; i += 4) {
            var lum = (0.299 * d[i] + 0.587 * d[i + 1] + 0.114 * d[i + 2]) * d[i + 3] / 255 + (255 - d[i + 3]);
            var a = lum < 110 ? 255 : lum < 170 ? Math.round((170 - lum) * 255 / 60) : 0;
            d[i] = 29; d[i + 1] = 27; d[i + 2] = 43; d[i + 3] = a;
            if (a > 0) encre++;
        }
        if (encre < 30) return null;
        g.putImageData(im, 0, 0);
        return recadrer(t);
    }

    // Recadre la signature autour du trait (elle remplit mieux la case du PDF) / Crop around the ink
    function recadrer(c) {
        var d = c.getContext("2d").getImageData(0, 0, c.width, c.height).data;
        var x0 = c.width, y0 = c.height, x1 = -1, y1 = -1;
        for (var y = 0; y < c.height; y++) {
            for (var x = 0; x < c.width; x++) {
                if (d[(y * c.width + x) * 4 + 3] > 0) {
                    if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
                }
            }
        }
        if (x1 < 0) return c;
        var marge = 6;
        x0 = Math.max(0, x0 - marge); y0 = Math.max(0, y0 - marge);
        x1 = Math.min(c.width - 1, x1 + marge); y1 = Math.min(c.height - 1, y1 + marge);
        var r = document.createElement("canvas");
        r.width = x1 - x0 + 1; r.height = y1 - y0 + 1;
        r.getContext("2d").drawImage(c, x0, y0, r.width, r.height, 0, 0, r.width, r.height);
        return r;
    }

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
