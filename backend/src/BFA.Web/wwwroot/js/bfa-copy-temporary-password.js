(function () {
    "use strict";

    function fallbackCopy(value) {
        var textarea = document.createElement("textarea");
        textarea.value = value;
        textarea.setAttribute("readonly", "");
        textarea.style.position = "fixed";
        textarea.style.opacity = "0";
        document.body.appendChild(textarea);
        textarea.select();
        var copied = document.execCommand("copy");
        textarea.remove();
        return copied;
    }

    function copyPassword(container, button) {
        var valueElement = container.querySelector("[data-temporary-password-value]");
        var feedback = container.querySelector("[data-copy-feedback]");
        if (!valueElement || !feedback) {
            return;
        }

        var value = valueElement.textContent.trim();
        var copy = window.navigator.clipboard && window.navigator.clipboard.writeText
            ? window.navigator.clipboard.writeText(value)
            : Promise.resolve(fallbackCopy(value));

        copy.then(function (copied) {
            if (copied === false) {
                throw new Error("copy-failed");
            }
            feedback.textContent = "Senha copiada";
            button.focus();
            window.setTimeout(function () {
                feedback.textContent = "";
            }, 2000);
        }).catch(function () {
            feedback.textContent = "Não foi possível copiar";
        });
    }

    document.querySelectorAll("[data-copy-temporary-password]").forEach(function (button) {
        button.addEventListener("click", function () {
            var container = button.closest("[data-temporary-password]");
            if (container) {
                copyPassword(container, button);
            }
        });
    });
})();
