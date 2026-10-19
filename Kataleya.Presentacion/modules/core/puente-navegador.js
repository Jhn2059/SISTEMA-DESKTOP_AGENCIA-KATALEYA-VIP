const TIEMPO_DE_ESPERA = 15000;
const URL_PREDETERMINADA = "http://localhost:5080";

export function estaEnNavegador() {
    if (globalThis.chrome?.webview) {
        return false;
    }

    const parametros = new URLSearchParams(location.search);
    if (parametros.get("puente") === "mock") {
        return true;
    }

    return location.hostname === "localhost" || location.hostname === "127.0.0.1";
}

export function crearPuenteDeNavegador() {
    const parametros = new URLSearchParams(location.search);
    const urlBase = (parametros.get("api") ?? URL_PREDETERMINADA).replace(/\/+$/, "");

    return {
        disponible() {
            return true;
        },

        enviar(operacion, datos) {
            if (operacion === "iniciarSesion") {
                return iniciarSesion(urlBase, datos);
            }

            if (operacion === "cerrarSesion") {
                return Promise.resolve(null);
            }

            return Promise.reject(crearError("Operación no disponible en el navegador.", "GEN-01"));
        }
    };
}

function crearError(mensaje, codigo) {
    const error = new Error(mensaje);
    error.codigo = codigo;
    return error;
}

async function iniciarSesion(urlBase, datos) {
    const controlador = new AbortController();
    const temporizador = setTimeout(() => controlador.abort(), TIEMPO_DE_ESPERA);

    try {
        const respuesta = await fetch(`${urlBase}/sesiones`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(datos),
            signal: controlador.signal
        });

        if (respuesta.status === 400) {
            return { cuenta: null, rechazo: null, finBloqueo: null, llave: null, venceLlave: null };
        }

        if (!respuesta.ok) {
            throw crearError(`El servidor respondió ${respuesta.status}.`, "GEN-01");
        }

        return await respuesta.json();
    } catch (error) {
        if (error?.name === "AbortError") {
            throw crearError("El servidor no responde.", "CON-02");
        }

        if (error instanceof TypeError) {
            const hayRed = navigator.onLine !== false;
            throw crearError(
                hayRed ? "El servidor no responde." : "No hay conexión a internet.",
                hayRed ? "CON-02" : "CON-01");
        }

        throw error;
    } finally {
        clearTimeout(temporizador);
    }
}
