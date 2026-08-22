(() => {
  "use strict";

  const formatarTamanho = (bytes) => {
    if (bytes >= 1024 * 1024) {
      return `${new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 1 }).format(bytes / 1024 / 1024)} MB`;
    }

    if (bytes >= 1024) {
      return `${new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 0 }).format(bytes / 1024)} KB`;
    }

    return `${bytes} bytes`;
  };

  document.querySelectorAll("[data-bfa-file-upload]").forEach((component) => {
    const form = component.closest("form");
    const input = component.querySelector("[data-bfa-upload-input]");
    const surface = component.querySelector("[data-bfa-file-upload-surface]");
    const name = component.querySelector("[data-bfa-file-upload-name]");
    const size = component.querySelector("[data-bfa-file-upload-size]");
    const action = component.querySelector("[data-bfa-file-upload-action]");
    const submit = form?.querySelector("[data-bfa-upload-submit]");

    if (!form || !input || !surface || !name || !size || !action || !submit) {
      return;
    }

    const emptyName = name.textContent;
    const emptySize = size.textContent;

    const update = () => {
      const file = input.files?.[0];
      component.classList.toggle("is-selected", Boolean(file));
      name.textContent = file?.name || emptyName;
      size.textContent = file ? formatarTamanho(file.size) : emptySize;
      action.textContent = file ? "Trocar arquivo" : "Selecionar";
      submit.disabled = !file;
    };

    input.addEventListener("change", update);

    ["dragenter", "dragover"].forEach((eventName) => {
      surface.addEventListener(eventName, (event) => {
        event.preventDefault();
        component.classList.add("is-dragging");
      });
    });

    ["dragleave", "drop"].forEach((eventName) => {
      surface.addEventListener(eventName, (event) => {
        event.preventDefault();
        component.classList.remove("is-dragging");
      });
    });

    surface.addEventListener("drop", (event) => {
      const file = event.dataTransfer?.files?.[0];

      if (!file) {
        return;
      }

      if (typeof DataTransfer === "undefined") {
        return;
      }

      const transfer = new DataTransfer();
      transfer.items.add(file);
      input.files = transfer.files;
      update();
    });

    form.addEventListener("submit", () => {
      submit.disabled = true;
      submit.textContent = "Enviando…";
    });

    update();
  });
})();
