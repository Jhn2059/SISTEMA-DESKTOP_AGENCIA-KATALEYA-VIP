import { puente } from "../core/puente.js";

const CLAVE_CUENTA = "kataleya.cuenta";

export function iniciarSesion(nombreUsuario, contrasena) {
    return puente.enviar("iniciarSesion", { nombreUsuario, contrasena });
}

export function cerrarSesion() {
    return puente.enviar("cerrarSesion");
}

export function guardarCuenta(cuenta) {
    sessionStorage.setItem(CLAVE_CUENTA, JSON.stringify(cuenta));
}

export function leerCuenta() {
    const texto = sessionStorage.getItem(CLAVE_CUENTA);
    if (!texto) {
        return null;
    }

    try {
        return JSON.parse(texto);
    } catch {
        sessionStorage.removeItem(CLAVE_CUENTA);
        return null;
    }
}

export function limpiarCuenta() {
    sessionStorage.removeItem(CLAVE_CUENTA);
}
