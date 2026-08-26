(() => {
    "use strict";

    const meses = [
        "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho",
        "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro"
    ];
    const diasSemana = ["D", "S", "T", "Q", "Q", "S", "S"];
    let calendarioAberto = null;

    const formatarDigitacao = (valor) => {
        const digitos = valor.replace(/\D/g, "").slice(0, 8);
        if (digitos.length <= 2) return digitos;
        if (digitos.length <= 4) return `${digitos.slice(0, 2)}/${digitos.slice(2)}`;
        return `${digitos.slice(0, 2)}/${digitos.slice(2, 4)}/${digitos.slice(4)}`;
    };

    const analisarData = (valor) => {
        const partes = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(valor.trim());
        if (!partes) return null;

        const data = { dia: Number(partes[1]), mes: Number(partes[2]), ano: Number(partes[3]) };
        const verificacao = new Date(data.ano, data.mes - 1, data.dia, 12);
        return verificacao.getFullYear() === data.ano
            && verificacao.getMonth() === data.mes - 1
            && verificacao.getDate() === data.dia
            ? data
            : null;
    };

    const formatarData = (data) => `${String(data.dia).padStart(2, "0")}/${String(data.mes).padStart(2, "0")}/${String(data.ano).padStart(4, "0")}`;
    const analisarDataIso = (valor) => {
        const partes = /^(\d{4})-(\d{2})-(\d{2})$/.exec(valor ?? "");
        return partes ? { dia: Number(partes[3]), mes: Number(partes[2]), ano: Number(partes[1]) } : null;
    };
    const compararDatas = (a, b) => new Date(a.ano, a.mes - 1, a.dia, 12)
        - new Date(b.ano, b.mes - 1, b.dia, 12);
    const hoje = () => {
        const agora = new Date();
        return { dia: agora.getDate(), mes: agora.getMonth() + 1, ano: agora.getFullYear() };
    };
    const mesmaData = (a, b) => Boolean(a && b)
        && a.dia === b.dia && a.mes === b.mes && a.ano === b.ano;

    const atualizarValidacao = (input) => {
        if (!window.jQuery?.validator) return;
        const campo = window.jQuery(input);
        if (campo.closest("form").data("validator")) campo.valid();
    };

    const instalarValidacaoPtBr = () => {
        if (!window.jQuery?.validator || window.jQuery.validator.methods.bfaDataPtBr) return;
        window.jQuery.validator.addMethod("bfaDataPtBr", function (valor, elemento) {
            return this.optional(elemento) || analisarData(valor) !== null;
        }, "Informe uma data válida no formato dd/mm/aaaa.");
    };

    const criarBotaoIcone = (rotulo, caminho) => {
        const botao = document.createElement("button");
        botao.type = "button";
        botao.className = "bfa-admin-calendar__nav";
        botao.setAttribute("aria-label", rotulo);
        botao.title = rotulo;
        botao.innerHTML = `<svg viewBox="0 0 24 24" aria-hidden="true" focusable="false"><path d="${caminho}"></path></svg>`;
        return botao;
    };

    let proximoIndiceCalendario = 0;

    const iniciarCampo = (campo) => {
        if (campo.dataset.bfaDateInitialized === "true") return;
        campo.dataset.bfaDateInitialized = "true";
        const entrada = campo.querySelector("[data-bfa-date-input]");
        const gatilho = campo.querySelector("[data-bfa-date-trigger]");
        if (!entrada || !gatilho) return;
        const dataMinima = analisarDataIso(entrada.dataset.bfaDateMin);

        const calendario = document.createElement("div");
        const indice = proximoIndiceCalendario++;
        const tituloId = `bfa-calendar-title-${indice}`;
        const calendarioId = `bfa-calendar-${indice}`;
        calendario.id = calendarioId;
        calendario.className = "bfa-admin-calendar";
        calendario.hidden = true;
        calendario.setAttribute("role", "dialog");
        calendario.setAttribute("aria-modal", "false");
        calendario.setAttribute("aria-labelledby", tituloId);
        gatilho.setAttribute("aria-controls", calendarioId);

        const cabecalho = document.createElement("div");
        cabecalho.className = "bfa-admin-calendar__header";
        const anterior = criarBotaoIcone("Mês anterior", "m15 18-6-6 6-6");
        const titulo = document.createElement("p");
        titulo.id = tituloId;
        titulo.className = "bfa-admin-calendar__title";
        titulo.setAttribute("aria-live", "polite");
        const proximo = criarBotaoIcone("Próximo mês", "m9 18 6-6-6-6");
        cabecalho.append(anterior, titulo, proximo);

        const semana = document.createElement("div");
        semana.className = "bfa-admin-calendar__weekdays";
        semana.setAttribute("aria-hidden", "true");
        diasSemana.forEach((dia) => {
            const item = document.createElement("span");
            item.textContent = dia;
            semana.append(item);
        });

        const grade = document.createElement("div");
        grade.className = "bfa-admin-calendar__grid";
        grade.setAttribute("role", "grid");
        grade.setAttribute("aria-label", "Dias do mês");

        const rodape = document.createElement("div");
        rodape.className = "bfa-admin-calendar__footer";
        const botaoHoje = document.createElement("button");
        botaoHoje.type = "button";
        botaoHoje.className = "bfa-admin-calendar__today";
        botaoHoje.textContent = "Hoje";
        rodape.append(botaoHoje);
        calendario.append(cabecalho, semana, grade, rodape);
        campo.append(calendario);

        const inicial = analisarData(entrada.value) ?? hoje();
        let mesExibido = inicial.mes;
        let anoExibido = inicial.ano;

        const fechar = (devolverFoco = false) => {
            calendario.hidden = true;
            gatilho.setAttribute("aria-expanded", "false");
            campo.classList.remove("opens-up", "aligns-right");
            calendario.style.removeProperty("--bfa-calendar-offset-x");
            calendario.style.removeProperty("--bfa-calendar-offset-y");
            if (calendarioAberto?.calendario === calendario) calendarioAberto = null;
            if (devolverFoco) gatilho.focus();
        };

        const selecionar = (data) => {
            entrada.value = formatarData(data);
            entrada.dispatchEvent(new Event("input", { bubbles: true }));
            entrada.dispatchEvent(new Event("change", { bubbles: true }));
            atualizarValidacao(entrada);
            fechar();
            entrada.focus();
        };

        const focarData = (data) => {
            const seletor = `[data-bfa-calendar-date="${data.ano}-${data.mes}-${data.dia}"]`;
            globalThis.requestAnimationFrame(() => grade.querySelector(seletor)?.focus());
        };

        const renderizar = () => {
            const selecionada = analisarData(entrada.value);
            const dataHoje = hoje();
            const quantidadeDias = new Date(anoExibido, mesExibido, 0).getDate();
            const primeiroDia = new Date(anoExibido, mesExibido - 1, 1).getDay();
            titulo.textContent = `${meses[mesExibido - 1]} de ${anoExibido}`;
            grade.replaceChildren();

            for (let vazio = 0; vazio < primeiroDia; vazio += 1) {
                const espaco = document.createElement("span");
                espaco.className = "bfa-admin-calendar__empty-day";
                espaco.setAttribute("aria-hidden", "true");
                grade.append(espaco);
            }

            for (let dia = 1; dia <= quantidadeDias; dia += 1) {
                const data = { dia, mes: mesExibido, ano: anoExibido };
                const botao = document.createElement("button");
                botao.type = "button";
                botao.className = "bfa-admin-calendar__day";
                botao.textContent = String(dia);
                botao.dataset.bfaCalendarDate = `${data.ano}-${data.mes}-${data.dia}`;
                botao.setAttribute("aria-label", `${dia} de ${meses[mesExibido - 1].toLowerCase()} de ${anoExibido}`);
                botao.setAttribute("aria-selected", mesmaData(data, selecionada) ? "true" : "false");

                if (dataMinima && compararDatas(data, dataMinima) < 0) {
                    botao.disabled = true;
                    botao.setAttribute("aria-label", `${botao.getAttribute("aria-label")}, indisponível`);
                }

                if (mesmaData(data, dataHoje)) {
                    botao.classList.add("is-today");
                    botao.setAttribute("aria-current", "date");
                }
                if (mesmaData(data, selecionada)) botao.classList.add("is-selected");

                botao.addEventListener("click", () => selecionar(data));
                botao.addEventListener("keydown", (evento) => {
                    const deslocamentos = { ArrowLeft: -1, ArrowRight: 1, ArrowUp: -7, ArrowDown: 7 };
                    const deslocamento = deslocamentos[evento.key];
                    if (deslocamento === undefined) return;

                    evento.preventDefault();
                    const proxima = new Date(data.ano, data.mes - 1, data.dia + deslocamento, 12);
                    const destino = { dia: proxima.getDate(), mes: proxima.getMonth() + 1, ano: proxima.getFullYear() };
                    mesExibido = destino.mes;
                    anoExibido = destino.ano;
                    renderizar();
                    focarData(destino);
                });
                grade.append(botao);
            }
        };

        if (dataMinima && compararDatas(hoje(), dataMinima) < 0) botaoHoje.disabled = true;

        const posicionar = () => {
            campo.classList.remove("opens-up", "aligns-right");
            calendario.style.setProperty("--bfa-calendar-offset-x", "0px");
            calendario.style.setProperty("--bfa-calendar-offset-y", "0px");
            let retangulo = calendario.getBoundingClientRect();
            if (retangulo.right > window.innerWidth - 8) campo.classList.add("aligns-right");

            const campoRect = campo.getBoundingClientRect();
            const espacoAbaixo = window.innerHeight - campoRect.bottom;
            const espacoAcima = campoRect.top;
            if (retangulo.height > espacoAbaixo && espacoAcima > espacoAbaixo) campo.classList.add("opens-up");

            retangulo = calendario.getBoundingClientRect();
            const deslocamentoX = retangulo.left < 8
                ? 8 - retangulo.left
                : Math.min(0, window.innerWidth - 8 - retangulo.right);
            const deslocamentoY = retangulo.top < 8
                ? 8 - retangulo.top
                : Math.min(0, window.innerHeight - 8 - retangulo.bottom);
            calendario.style.setProperty("--bfa-calendar-offset-x", `${deslocamentoX}px`);
            calendario.style.setProperty("--bfa-calendar-offset-y", `${deslocamentoY}px`);
        };

        const abrir = () => {
            if (!calendario.hidden) {
                fechar();
                return;
            }

            calendarioAberto?.fechar();
            const informada = analisarData(entrada.value) ?? hoje();
            mesExibido = informada.mes;
            anoExibido = informada.ano;
            renderizar();
            calendario.hidden = false;
            gatilho.setAttribute("aria-expanded", "true");
            calendarioAberto = { calendario, fechar };
            globalThis.requestAnimationFrame(posicionar);
        };

        anterior.addEventListener("click", () => {
            const data = new Date(anoExibido, mesExibido - 2, 1, 12);
            mesExibido = data.getMonth() + 1;
            anoExibido = data.getFullYear();
            renderizar();
        });
        proximo.addEventListener("click", () => {
            const data = new Date(anoExibido, mesExibido, 1, 12);
            mesExibido = data.getMonth() + 1;
            anoExibido = data.getFullYear();
            renderizar();
        });
        botaoHoje.addEventListener("click", () => selecionar(hoje()));
        gatilho.addEventListener("click", abrir);
        entrada.addEventListener("click", () => {
            if (calendario.hidden) abrir();
        });

        entrada.addEventListener("input", () => {
            entrada.value = formatarDigitacao(entrada.value);
            const informada = analisarData(entrada.value);
            if (informada && !calendario.hidden) {
                mesExibido = informada.mes;
                anoExibido = informada.ano;
                renderizar();
            }
            if (entrada.classList.contains("input-validation-error")) atualizarValidacao(entrada);
        });
        entrada.addEventListener("blur", () => atualizarValidacao(entrada));
        entrada.addEventListener("keydown", (evento) => {
            if (evento.key === "ArrowDown" && evento.altKey) {
                evento.preventDefault();
                abrir();
            }
        });

        const campoJquery = window.jQuery?.validator ? window.jQuery(entrada) : null;
        if (campoJquery?.closest("form").data("validator")) {
            const regras = {
                bfaDataPtBr: true,
                messages: { bfaDataPtBr: entrada.dataset.bfaDateInvalidMessage }
            };
            if (entrada.dataset.bfaDateRequired === "true") regras.required = "Informe a data de início.";
            campoJquery.rules("add", regras);
        }
        renderizar();
    };

    const iniciar = () => {
        instalarValidacaoPtBr();
        document.querySelectorAll("[data-bfa-date-field]").forEach(iniciarCampo);
        document.addEventListener("pointerdown", (evento) => {
            if (calendarioAberto && !calendarioAberto.calendario.parentElement.contains(evento.target)) {
                calendarioAberto.fechar();
            }
        });
        document.addEventListener("keydown", (evento) => {
            if (evento.key === "Escape" && calendarioAberto) calendarioAberto.fechar(true);
        });
    };

    window.BfaDateField = {
        iniciar: (raiz = document) => {
            instalarValidacaoPtBr();
            raiz.querySelectorAll("[data-bfa-date-field]").forEach(iniciarCampo);
        }
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", iniciar, { once: true });
    } else {
        iniciar();
    }
})();
