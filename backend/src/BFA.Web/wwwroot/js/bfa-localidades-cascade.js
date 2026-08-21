(() => {
    "use strict";

    const iniciar = () => {
        document.querySelectorAll("[data-bfa-localidades]").forEach((container) => {
            const estado = container.querySelector("[data-bfa-localidade-estado]");
            const municipio = container.querySelector("[data-bfa-localidade-municipio]");
            const status = container.querySelector("[data-bfa-localidades-status]");
            const tentarNovamente = container.querySelector("[data-bfa-localidades-retry]");
            const endpoint = container.dataset.municipiosUrl;

            if (!estado?.bfaCombobox || !municipio?.bfaCombobox || !endpoint) {
                return;
            }

            let requisicaoAtual;

            const informar = (mensagem, erro = false) => {
                status.textContent = mensagem;
                status.classList.toggle("is-error", erro);
            };

            const prepararMunicipio = (placeholder) => {
                municipio.bfaCombobox.replaceOptions([], placeholder);
                municipio.bfaCombobox.setPlaceholder(placeholder);
                municipio.bfaCombobox.setDisabled(true);
            };

            const carregarMunicipios = async () => {
                requisicaoAtual?.abort();
                requisicaoAtual = undefined;
                tentarNovamente.hidden = true;
                informar("");
                prepararMunicipio("Selecione primeiro o Estado");

                if (!estado.value) {
                    return;
                }

                const estadoCodigoIbge = estado.value;
                const controle = new AbortController();
                requisicaoAtual = controle;
                const requisicaoAindaAtual = () =>
                    requisicaoAtual === controle && estado.value === estadoCodigoIbge;
                prepararMunicipio("Carregando municípios...");
                informar("Carregando municípios...");

                try {
                    const url = new URL(endpoint, window.location.origin);
                    url.searchParams.set("estadoCodigoIbge", estadoCodigoIbge);
                    const resposta = await fetch(url, {
                        method: "GET",
                        headers: { Accept: "application/json" },
                        signal: controle.signal
                    });

                    if (!requisicaoAindaAtual()) {
                        return;
                    }

                    if (!resposta.ok) {
                        throw new Error("Falha ao carregar municípios.");
                    }

                    const dados = await resposta.json();

                    if (!requisicaoAindaAtual()) {
                        return;
                    }

                    if (!Array.isArray(dados)) {
                        throw new Error("Resposta inválida ao carregar municípios.");
                    }

                    const opcoes = dados
                        .filter((item) => Number.isInteger(item.codigoIbge)
                            && item.codigoIbge > 0
                            && typeof item.nome === "string"
                            && item.nome.trim())
                        .map((item) => ({ value: item.codigoIbge, label: item.nome.trim() }));

                    if (opcoes.length === 0) {
                        prepararMunicipio("Nenhum Município disponível");
                        informar("Não há Municípios ativos para o Estado selecionado.", true);
                        tentarNovamente.hidden = false;
                        return;
                    }

                    municipio.bfaCombobox.replaceOptions(
                        opcoes,
                        "Pesquise ou selecione um Município");
                    municipio.bfaCombobox.setPlaceholder(
                        "Pesquise ou selecione um Município");
                    municipio.bfaCombobox.setDisabled(false);
                    informar("");
                } catch (error) {
                    if (controle.signal.aborted || !requisicaoAindaAtual()) {
                        return;
                    }

                    prepararMunicipio("Selecione novamente o Estado");
                    informar("Não foi possível carregar os municípios. Tente novamente.", true);
                    tentarNovamente.hidden = false;
                } finally {
                    if (requisicaoAtual === controle) {
                        requisicaoAtual = undefined;
                    }
                }
            };

            estado.addEventListener("change", carregarMunicipios);
            tentarNovamente.addEventListener("click", carregarMunicipios);
        });
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", iniciar, { once: true });
    } else {
        iniciar();
    }
})();
