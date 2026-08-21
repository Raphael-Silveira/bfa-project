(() => {
    "use strict";

    const normalizar = (valor) => valor
        .normalize("NFD")
        .replace(/[\u0300-\u036f]/g, "")
        .toLocaleLowerCase("pt-BR")
        .trim();
    let proximoIdentificador = 0;

    class BfaSearchableCombobox {
        constructor(select) {
            this.select = select;
            this.opcoesVisiveis = [];
            this.indiceAtivo = -1;
            proximoIdentificador += 1;
            this.identificador = select.id || `bfa-combobox-${proximoIdentificador}`;
            this.criarInterface();
            this.registrarEventos();
            this.sincronizarSelecao();
            this.setDisabled(select.disabled);
            select.bfaCombobox = this;
        }

        criarInterface() {
            this.container = document.createElement("div");
            this.container.className = "bfa-admin-combobox";

            this.input = document.createElement("input");
            this.input.type = "text";
            this.input.id = `${this.identificador}Pesquisa`;
            this.input.className = "form-control bfa-form-control bfa-admin-combobox__input";
            this.input.placeholder = this.select.dataset.placeholder || "Pesquise ou selecione";
            this.input.autocomplete = "off";
            this.input.setAttribute("role", "combobox");
            this.input.setAttribute("aria-autocomplete", "list");
            this.input.setAttribute("aria-haspopup", "listbox");
            this.input.setAttribute("aria-expanded", "false");
            this.input.setAttribute("aria-required", this.select.required.toString());
            const invalido = this.select.classList.contains("input-validation-error");
            this.input.classList.toggle("input-validation-error", invalido);

            if (invalido) {
                this.input.setAttribute("aria-invalid", "true");
            }

            this.lista = document.createElement("div");
            this.lista.id = `${this.identificador}Lista`;
            this.lista.className = "bfa-admin-combobox__list";
            this.lista.hidden = true;
            this.lista.setAttribute("role", "listbox");
            this.input.setAttribute("aria-controls", this.lista.id);

            this.container.append(this.input, this.lista);
            this.select.insertAdjacentElement("afterend", this.container);
            this.select.classList.add("bfa-admin-combobox__native", "is-enhanced");
            this.select.tabIndex = -1;
            this.select.setAttribute("aria-hidden", "true");

            const label = [...document.querySelectorAll("label[for]")]
                .find((item) => item.htmlFor === this.identificador);
            label?.setAttribute("for", this.input.id);
        }

        registrarEventos() {
            this.input.addEventListener("focus", () => this.abrir(""));
            this.input.addEventListener("click", () => this.abrir(""));
            this.input.addEventListener("input", () => {
                const valorDigitado = this.input.value;

                if (this.select.value) {
                    this.select.value = "";
                    this.select.dispatchEvent(new Event("change", { bubbles: true }));
                    this.input.value = valorDigitado;
                }

                this.abrir(valorDigitado);
            });
            this.input.addEventListener("keydown", (event) => this.tratarTeclado(event));
            this.select.addEventListener("change", () => this.sincronizarSelecao());
            document.addEventListener("pointerdown", (event) => {
                if (!this.container.contains(event.target) && event.target !== this.select) {
                    this.fechar();
                }
            });
        }

        tratarTeclado(event) {
            if (event.key === "ArrowDown") {
                event.preventDefault();
                this.abrir(this.lista.hidden ? "" : this.input.value);
                this.moverAtivo(1);
                return;
            }

            if (event.key === "ArrowUp") {
                event.preventDefault();
                this.abrir(this.lista.hidden ? "" : this.input.value);
                this.moverAtivo(-1);
                return;
            }

            if (event.key === "Enter" && !this.lista.hidden && this.indiceAtivo >= 0) {
                event.preventDefault();
                this.selecionar(this.opcoesVisiveis[this.indiceAtivo]);
                return;
            }

            if (event.key === "Escape") {
                event.preventDefault();
                this.fechar();
                return;
            }

            if (event.key === "Tab") {
                this.fechar();
            }
        }

        abrir(filtro = "") {
            if (this.input.disabled) {
                return;
            }

            this.renderizar(filtro);
            this.posicionarLista();
            this.lista.hidden = false;
            this.input.setAttribute("aria-expanded", "true");
        }

        fechar() {
            this.lista.hidden = true;
            this.input.setAttribute("aria-expanded", "false");
            this.input.removeAttribute("aria-activedescendant");
            this.indiceAtivo = -1;
        }

        posicionarLista() {
            const retangulo = this.input.getBoundingClientRect();
            const alturaViewport = window.visualViewport?.height ?? window.innerHeight;
            const espacoAbaixo = alturaViewport - retangulo.bottom;
            const espacoAcima = retangulo.top;
            const abrirAcima = espacoAbaixo < 280 && espacoAcima > espacoAbaixo;
            const espacoDisponivel = abrirAcima ? espacoAcima : espacoAbaixo;
            this.container.classList.toggle("opens-up", abrirAcima);
            this.container.style.setProperty(
                "--bfa-combobox-available-height",
                `${Math.max(96, espacoDisponivel - 16)}px`);
        }

        renderizar(filtro = "") {
            const termo = normalizar(filtro);
            const opcoes = [...this.select.options].filter((opcao) =>
                opcao.value && normalizar(opcao.textContent || "").includes(termo));
            this.lista.replaceChildren();
            this.opcoesVisiveis = [];
            this.indiceAtivo = -1;

            if (opcoes.length === 0) {
                const vazio = document.createElement("p");
                vazio.className = "bfa-admin-combobox__empty";
                vazio.textContent = this.select.dataset.emptyMessage || "Nenhuma opção encontrada.";
                this.lista.append(vazio);
                return;
            }

            opcoes.forEach((opcao, indice) => {
                const item = document.createElement("button");
                item.type = "button";
                item.id = `${this.identificador}Opcao${indice}`;
                item.className = "bfa-admin-combobox__option";
                item.textContent = opcao.textContent;
                item.dataset.value = opcao.value;
                item.setAttribute("role", "option");
                item.setAttribute("aria-selected", (opcao.value === this.select.value).toString());
                item.addEventListener("pointerdown", (event) => event.preventDefault());
                item.addEventListener("click", () => this.selecionar(item));
                this.lista.append(item);
                this.opcoesVisiveis.push(item);
            });
        }

        moverAtivo(deslocamento) {
            if (this.opcoesVisiveis.length === 0) {
                return;
            }

            this.indiceAtivo = this.indiceAtivo < 0
                ? (deslocamento > 0 ? 0 : this.opcoesVisiveis.length - 1)
                : (this.indiceAtivo + deslocamento + this.opcoesVisiveis.length)
                    % this.opcoesVisiveis.length;
            this.opcoesVisiveis.forEach((opcao, indice) => {
                opcao.classList.toggle("is-active", indice === this.indiceAtivo);
            });
            const ativa = this.opcoesVisiveis[this.indiceAtivo];
            this.input.setAttribute("aria-activedescendant", ativa.id);
            ativa.scrollIntoView({ block: "nearest" });
        }

        selecionar(item) {
            this.select.value = item.dataset.value || "";
            this.sincronizarSelecao();
            this.select.dispatchEvent(new Event("change", { bubbles: true }));
            this.fechar();
            this.input.focus();
        }

        sincronizarSelecao() {
            const selecionada = this.select.selectedOptions[0];
            this.input.value = selecionada?.value ? selecionada.textContent.trim() : "";
        }

        clear() {
            this.select.value = "";
            this.input.value = "";
            this.fechar();
        }

        replaceOptions(opcoes, placeholder) {
            this.select.replaceChildren();
            const opcaoInicial = document.createElement("option");
            opcaoInicial.value = "";
            opcaoInicial.textContent = placeholder;
            this.select.append(opcaoInicial);

            opcoes.forEach((opcao) => {
                const element = document.createElement("option");
                element.value = String(opcao.value);
                element.textContent = opcao.label;
                this.select.append(element);
            });
            this.clear();
        }

        setDisabled(disabled) {
            this.select.disabled = disabled;
            this.input.disabled = disabled;
            this.input.setAttribute("aria-disabled", disabled.toString());

            if (disabled) {
                this.fechar();
            }
        }

        setPlaceholder(placeholder) {
            this.input.placeholder = placeholder;
        }
    }

    const iniciar = () => {
        document.querySelectorAll("select[data-bfa-combobox]").forEach((select) => {
            if (!select.bfaCombobox) {
                new BfaSearchableCombobox(select);
            }
        });
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", iniciar, { once: true });
    } else {
        iniciar();
    }
})();
