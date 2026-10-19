import { crearPuenteDeNavegador, estaEnNavegador } from "./puente-navegador.js";

const TIEMPO_DE_ESPERA = 15000;

function crearPuenteDeEscritorio() {
    const pendientes = new Map();
    let siguienteId = 1;

    function obtenerWebView() {
        return globalThis.chrome?.webview ?? null;
    }

    function crearError(mensaje, codigo) {
        const error = new Error(mensaje);
        error.codigo = codigo;
        return error;
    }

    function recibirRespuesta(evento) {
        let respuesta = evento.data;

        if (typeof respuesta === "string") {
            try {
                respuesta = JSON.parse(respuesta);
            } catch {
                return;
            }
        }

        if (!respuesta || typeof respuesta.id !== "string") {
            return;
        }

        const pendiente = pendientes.get(respuesta.id);
        if (!pendiente) {
            return;
        }

        clearTimeout(pendiente.temporizador);
        pendientes.delete(respuesta.id);

        if (respuesta.error) {
            pendiente.rechazar(crearError(respuesta.error, "GEN-01"));
            return;
        }

        pendiente.resolver(respuesta.resultado);
    }

    const webView = obtenerWebView();
    if (webView) {
        webView.addEventListener("message", recibirRespuesta);
    }

    function enviar(operacion, datos) {
        return new Promise((resolver, rechazar) => {
            const vista = obtenerWebView();
            if (!vista) {
                rechazar(crearError("La aplicación de escritorio no está disponible.", "GEN-01"));
                return;
            }

            const id = String(siguienteId);
            siguienteId += 1;

            const temporizador = setTimeout(() => {
                pendientes.delete(id);
                rechazar(crearError("El servidor no responde.", "CON-02"));
            }, TIEMPO_DE_ESPERA);

            pendientes.set(id, { resolver, rechazar, temporizador });

            vista.postMessage(JSON.stringify({ id, operacion, datos }));
        });
    }

    return {
        disponible() {
            return Boolean(obtenerWebView());
        },

        enviar
    };
}

export const puente = estaEnNavegador() ? crearPuenteDeNavegador() : crearPuenteDeEscritorio();
