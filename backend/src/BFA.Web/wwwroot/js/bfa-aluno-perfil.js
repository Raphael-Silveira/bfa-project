(() => {
    "use strict";
    const iniciar = () => {
        const estado = document.querySelector("[data-aluno-estado]");
        const municipio = document.querySelector("[data-aluno-municipio]");
        const fotoInput = document.querySelector("[data-foto-perfil-input]");
        let previewUrl;

        if (fotoInput) {
            fotoInput.addEventListener("change", () => {
                const arquivo = fotoInput.files?.[0];
                if (!arquivo || !arquivo.type.startsWith("image/")) return;

                const existente = document.querySelector("#foto-perfil-preview");
                const fallback = document.querySelector("#foto-perfil-fallback");
                if (previewUrl) URL.revokeObjectURL(previewUrl);
                previewUrl = URL.createObjectURL(arquivo);
                if (existente) {
                    existente.src = previewUrl;
                } else {
                    const imagem = document.createElement("img");
                    imagem.id = "foto-perfil-preview";
                    imagem.className = "bfa-aluno-profile-avatar";
                    imagem.alt = "Prévia da nova foto de perfil";
                    imagem.src = previewUrl;
                    fallback?.replaceWith(imagem);
                }
            });
        }

        if (!estado || !municipio?.bfaCombobox) return;

        let requisicaoAtual;
        const prepararMunicipio = (placeholder) => {
            municipio.bfaCombobox.replaceOptions([], placeholder);
            municipio.bfaCombobox.setPlaceholder(placeholder);
            municipio.bfaCombobox.setDisabled(true);
        };

        estado.addEventListener("change", async () => {
            requisicaoAtual?.abort();
            requisicaoAtual = undefined;
            const codigo = estado.value;
            prepararMunicipio(codigo ? "Carregando cidades..." : "Selecione um Estado primeiro");

            if (!codigo) return;

            const controle = new AbortController();
            requisicaoAtual = controle;
            try {
                const url = new URL(estado.dataset.municipioUrl, window.location.origin);
                url.searchParams.set("estadoCodigoIbge", codigo);
                const resposta = await fetch(url, {
                    headers: { "Accept": "application/json" },
                    signal: controle.signal
                });
                if (!resposta.ok) throw new Error("Falha ao carregar cidades");
                const itens = await resposta.json();
                if (requisicaoAtual !== controle || estado.value !== codigo) return;
                if (!Array.isArray(itens)) throw new Error("Resposta inválida");

                const opcoes = itens
                    .filter(item => Number.isInteger(item.codigoIbge) && item.codigoIbge > 0
                        && typeof item.nome === "string" && item.nome.trim())
                    .map(item => ({ value: item.codigoIbge, label: item.nome.trim() }));
                municipio.bfaCombobox.replaceOptions(opcoes, "Pesquise ou selecione uma Cidade");
                municipio.bfaCombobox.setDisabled(false);
            } catch (erro) {
                if (controle.signal.aborted || requisicaoAtual !== controle) return;
                prepararMunicipio("Não foi possível carregar as cidades");
            } finally {
                if (requisicaoAtual === controle) requisicaoAtual = undefined;
            }
        });
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", iniciar, { once: true });
    } else {
        iniciar();
    }
})();
