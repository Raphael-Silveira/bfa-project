(() => {
    "use strict";

    /* ── Injeta CSS do modal no <head> uma unica vez ──────── */
    (function injectStyles() {
        if (document.getElementById("bfa-modal-styles")) return;
        var style = document.createElement("style");
        style.id = "bfa-modal-styles";
        style.textContent = [
            ".bfa-modal-overlay{position:fixed;inset:0;z-index:99999;display:flex;align-items:center;justify-content:center;padding:1rem;background:rgba(5,6,8,.82);backdrop-filter:blur(6px) saturate(120%);-webkit-backdrop-filter:blur(6px) saturate(120%);}",
            ".bfa-modal-panel{position:relative;width:min(480px,100%);max-height:calc(100vh - 2rem);overflow-y:auto;background:#1A1D1F;border:1px solid rgba(255,255,255,.09);border-top:2px solid #FFC107;border-radius:1rem;box-shadow:0 0 0 1px rgba(255,193,7,.07),0 8px 32px rgba(0,0,0,.55),0 24px 64px rgba(0,0,0,.45);color:#F7F7F7;}",
            ".bfa-modal-panel__header{display:flex;align-items:center;gap:.875rem;padding:1.375rem 1.5rem 1rem;}",
            ".bfa-modal-panel__icon{display:grid;place-items:center;flex-shrink:0;width:2.75rem;height:2.75rem;border-radius:50%;border:1px solid rgba(255,193,7,.22);background:rgba(255,193,7,.10);color:#FFC107;}",
            ".bfa-modal-panel__icon svg{width:20px;height:20px;}",
            ".bfa-modal-panel__icon--success{border-color:rgba(34,197,94,.22);background:rgba(34,197,94,.10);color:#22C55E;}",
            ".bfa-modal-panel__icon--danger{border-color:rgba(239,68,68,.22);background:rgba(239,68,68,.10);color:#EF4444;}",
            ".bfa-modal-panel__title{margin:0;font-size:1.05rem;font-weight:700;color:#fff;letter-spacing:-.015em;line-height:1.25;}",
            ".bfa-modal-panel__divider{height:1px;background:rgba(255,255,255,.07);margin:0 1.5rem;}",
            ".bfa-modal-panel__body{padding:1.125rem 1.5rem 1.25rem;}",
            ".bfa-modal-panel__question{margin:0 0 .5rem;font-size:.9375rem;font-weight:600;color:#F0F0F0;line-height:1.4;}",
            ".bfa-modal-panel__question:only-child{margin-bottom:0;}",
            ".bfa-modal-panel__desc{margin:0;font-size:.84375rem;color:#A0A4A8;line-height:1.6;}",
            ".bfa-modal-panel__footer{display:flex;justify-content:flex-end;align-items:center;gap:.625rem;padding:1rem 1.5rem;border-top:1px solid rgba(255,255,255,.07);background:rgba(0,0,0,.20);border-radius:0 0 1rem 1rem;}",
            ".bfa-modal-panel__footer .bfa-btn-secondary{background:rgba(255,255,255,.04)!important;border:1px solid rgba(255,255,255,.14)!important;color:#B0B4B8!important;font-weight:600!important;}",
            ".bfa-modal-panel__footer .bfa-btn-secondary:hover{background:rgba(255,255,255,.09)!important;border-color:rgba(255,255,255,.28)!important;color:#fff!important;}",
            ".bfa-modal-panel__footer .bfa-btn-primary{background:#FFC107!important;border:1px solid #FFC107!important;color:#0D0D0D!important;font-weight:700!important;}",
            ".bfa-modal-panel__footer .bfa-btn-primary:hover{background:#FFCA28!important;border-color:#FFCA28!important;box-shadow:0 0 18px rgba(255,193,7,.38)!important;}",
            ".bfa-modal-panel__footer .bfa-modal-panel__confirm--danger{background:#EF4444!important;border:1px solid #EF4444!important;color:#fff!important;font-weight:700!important;}",
            ".bfa-modal-panel__footer .bfa-modal-panel__confirm--danger:hover{background:#DC2626!important;border-color:#DC2626!important;box-shadow:0 0 18px rgba(239,68,68,.38)!important;}",
            "@media(max-width:30rem){",
                ".bfa-modal-overlay{align-items:flex-end;padding:0;}",
                ".bfa-modal-panel{width:100%;border-radius:1rem 1rem 0 0;border-top-width:3px;max-height:92svh;}",
                ".bfa-modal-panel__footer{flex-direction:column-reverse;border-radius:0;}",
                ".bfa-modal-panel__footer .bfa-btn-primary,.bfa-modal-panel__footer .bfa-btn-secondary,.bfa-modal-panel__footer .bfa-modal-panel__confirm--danger{width:100%!important;min-height:2.75rem!important;}",
            "}"
        ].join("\n");
        document.head.appendChild(style);
    })();

    /* ── Icones / classes por variante ───────────────────── */
    var ICON_PATHS = {
        danger:  "M12 9v4m0 4h.01M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z",
        success: "M22 11.08V12a10 10 0 1 1-5.93-9.14M22 4L12 14.01l-3-3",
        def:     "M9 12l2 2 4-4m6 2a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z"
    };
    var ICON_CLS = {
        danger:  "bfa-modal-panel__icon--danger",
        success: "bfa-modal-panel__icon--success",
        def:     ""
    };
    var CONFIRM_EXTRA = {
        danger:  " bfa-modal-panel__confirm--danger",
        success: "",
        def:     ""
    };

    function criarModal(titulo, mensagem, textoConfirmar, textoCancelar, variante, descricao) {
        var vk = ICON_PATHS[variante] ? variante : "def";

        /* Overlay */
        var overlay = document.createElement("div");
        overlay.className = "bfa-modal-overlay";
        overlay.setAttribute("role", "dialog");
        overlay.setAttribute("aria-modal", "true");
        overlay.setAttribute("aria-labelledby", "bfa-mpanel-title");
        if (descricao) overlay.setAttribute("aria-describedby", "bfa-mpanel-desc");

        /* Panel */
        var panel = document.createElement("div");
        panel.className = "bfa-modal-panel";

        var descHtml = descricao
            ? '<p class="bfa-modal-panel__desc" id="bfa-mpanel-desc">' + descricao + '</p>'
            : "";
        var bodyHtml = descricao
            ? '<p class="bfa-modal-panel__question">' + mensagem + '</p>' + descHtml
            : '<p class="bfa-modal-panel__question" id="bfa-mpanel-desc">' + mensagem + '</p>';
        var confirmCls = "bfa-btn-primary bfa-admin-button bfa-modal-panel__confirm" + CONFIRM_EXTRA[vk];

        panel.innerHTML =
            '<div class="bfa-modal-panel__header">' +
                '<div class="bfa-modal-panel__icon ' + ICON_CLS[vk] + '" aria-hidden="true">' +
                    '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">' +
                        '<path d="' + ICON_PATHS[vk] + '"/>' +
                    '</svg>' +
                '</div>' +
                '<h3 class="bfa-modal-panel__title" id="bfa-mpanel-title">' + titulo + '</h3>' +
            '</div>' +
            '<div class="bfa-modal-panel__divider" aria-hidden="true"></div>' +
            '<div class="bfa-modal-panel__body">' + bodyHtml + '</div>' +
            '<div class="bfa-modal-panel__footer">' +
                '<button class="bfa-btn-secondary bfa-admin-button bfa-modal-panel__cancel" type="button">' + textoCancelar + '</button>' +
                '<button class="' + confirmCls + '" type="button">' + textoConfirmar + '</button>' +
            '</div>';

        overlay.appendChild(panel);
        document.body.appendChild(overlay);

        /* Scroll lock */
        var scrollY = window.scrollY;
        document.body.style.position = "fixed";
        document.body.style.top = "-" + scrollY + "px";
        document.body.style.left = "0";
        document.body.style.right = "0";

        function unlockScroll() {
            document.body.style.position = "";
            document.body.style.top = "";
            document.body.style.left = "";
            document.body.style.right = "";
            window.scrollTo(0, scrollY);
        }

        /* Promise */
        var resolve;
        var promise = new Promise(function(r) { resolve = r; });

        function fechar(resultado) {
            unlockScroll();
            document.removeEventListener("keydown", onKeyDown);
            overlay.remove();
            resolve(resultado);
        }

        /* Focus trap + Escape */
        function onKeyDown(e) {
            if (e.key === "Escape") { e.preventDefault(); fechar(false); return; }
            if (e.key === "Tab") {
                var els = Array.from(panel.querySelectorAll(
                    "button,[href],input,select,textarea,[tabindex]:not([tabindex=\"-1\"])"
                )).filter(function(el) { return !el.disabled; });
                if (!els.length) { e.preventDefault(); return; }
                var first = els[0], last = els[els.length - 1];
                if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
                else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
            }
        }

        panel.querySelector(".bfa-modal-panel__cancel").addEventListener("click", function() { fechar(false); });
        panel.querySelector(".bfa-modal-panel__confirm").addEventListener("click", function() { fechar(true); });
        overlay.addEventListener("click", function(e) { if (e.target === overlay) fechar(false); });
        document.addEventListener("keydown", onKeyDown);

        requestAnimationFrame(function() {
            var btn = panel.querySelector(".bfa-modal-panel__confirm");
            if (btn) btn.focus();
        });

        return promise;
    }

    window.BfaConfirm = {
        confirm: function(opcoes) {
            return criarModal(
                opcoes.titulo         || "Confirmar",
                opcoes.mensagem        || "Tem certeza?",
                opcoes.textoConfirmar  || "Confirmar",
                opcoes.textoCancelar   || "Cancelar",
                opcoes.variante        || "default",
                opcoes.descricao       || null
            );
        }
    };
})();