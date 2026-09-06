(() => {
    "use strict";

    const criarModal = ({ titulo, mensagem, textoConfirmar, textoCancelar, variante, descricao }) => {
        const dialog = document.createElement("dialog");
        dialog.className = "bfa-modal-dialog";
        dialog.setAttribute("aria-labelledby", "bfa-modal-title");
        if (descricao) dialog.setAttribute("aria-describedby", "bfa-modal-desc");

        const iconPath = variante === "danger"
            ? "M12 9v4m0 4h.01M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z"
            : "M9 12l2 2 4-4m6 2a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z";

        const descHtml = descricao
            ? `<p id="bfa-modal-desc">${descricao}</p>`
            : `<p>${mensagem}</p>`;

        dialog.innerHTML = `
            <div class="bfa-modal">
                <div class="bfa-modal__header">
                    <div class="bfa-modal__icon ${variante === "danger" ? "bfa-modal__icon--danger" : ""}" aria-hidden="true">
                        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="${iconPath}"/></svg>
                    </div>
                    <h3 class="bfa-modal__title" id="bfa-modal-title">${titulo}</h3>
                </div>
                <div class="bfa-modal__body">
                    ${descricao ? `<p class="bfa-modal__message">${mensagem}</p>${descHtml}` : descHtml}
                </div>
                <div class="bfa-modal__footer">
                    <button class="bfa-btn-secondary bfa-admin-button bfa-modal__cancel" type="button">${textoCancelar}</button>
                    <button class="bfa-btn-primary bfa-admin-button bfa-modal__confirm" type="button">${textoConfirmar}</button>
                </div>
            </div>
        `;

        document.body.appendChild(dialog);

        let scrollY = 0;
        const lockScroll = () => {
            scrollY = window.scrollY;
            document.body.style.position = "fixed";
            document.body.style.top = `-${scrollY}px`;
            document.body.style.left = "0";
            document.body.style.right = "0";
        };
        const unlockScroll = () => {
            document.body.style.position = "";
            document.body.style.top = "";
            document.body.style.left = "";
            document.body.style.right = "";
            window.scrollTo(0, scrollY);
        };

        let resolve;
        const promise = new Promise((r) => { resolve = r; });

        const fechar = (resultado) => {
            if (dialog.open) dialog.close();
            unlockScroll();
            dialog.remove();
            resolve(resultado);
        };

        dialog.querySelector(".bfa-modal__cancel").addEventListener("click", () => fechar(false));
        dialog.querySelector(".bfa-modal__confirm").addEventListener("click", () => fechar(true));
        dialog.addEventListener("cancel", (e) => {
            e.preventDefault();
            fechar(false);
        });

        lockScroll();
        dialog.showModal();
        dialog.querySelector(".bfa-modal__confirm").focus();

        return promise;
    };

    window.BfaConfirm = {
        confirm: (opcoes) => criarModal({
            titulo: opcoes.titulo || "Confirmar",
            mensagem: opcoes.mensagem || "Tem certeza?",
            textoConfirmar: opcoes.textoConfirmar || "Confirmar",
            textoCancelar: opcoes.textoCancelar || "Cancelar",
            variante: opcoes.variante || "default",
            descricao: opcoes.descricao || null
        })
    };
})();
