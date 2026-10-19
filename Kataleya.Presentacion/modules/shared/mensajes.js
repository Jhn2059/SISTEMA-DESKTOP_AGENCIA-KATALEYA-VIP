const MENSAJES = {
    SES01: "El nombre de usuario o la contraseña no son correctos.",
    SES02: "La cuenta está bloqueada hasta las {hora}. Vuelva a intentarlo entonces.",
    SES03: "La cuenta está desactivada. Un administrador puede reactivarla.",
    CON01: "No hay conexión a internet. Revise la conexión e inténtelo de nuevo.",
    CON02: "El servidor no responde. Inténtelo de nuevo en unos minutos.",
    GEN01: "No se pudo completar la operación. Vuelva a intentarlo."
};

export const MENSAJE_SIN_PUENTE =
    "Abra esta pantalla desde la aplicación de escritorio Kataleya VIP.";

export function mensajeDeRechazo(rechazo, finBloqueo) {
    switch (rechazo) {
        case "DatosIncorrectos":
            return MENSAJES.SES01;
        case "CuentaBloqueada":
            return mensajeDeBloqueo(finBloqueo);
        case "CuentaDesactivada":
            return MENSAJES.SES03;
        case "SinInternet":
            return MENSAJES.CON01;
        case "ServidorNoResponde":
            return MENSAJES.CON02;
        default:
            return MENSAJES.GEN01;
    }
}

export function mensajeDeError(error) {
    switch (error?.codigo) {
        case "CON-01":
            return MENSAJES.CON01;
        case "CON-02":
            return MENSAJES.CON02;
        default:
            return MENSAJES.GEN01;
    }
}

function mensajeDeBloqueo(finBloqueo) {
    if (!finBloqueo) {
        return "La cuenta está bloqueada temporalmente. Vuelva a intentarlo en unos minutos.";
    }

    const fecha = new Date(finBloqueo);
    if (Number.isNaN(fecha.getTime())) {
        return "La cuenta está bloqueada temporalmente. Vuelva a intentarlo en unos minutos.";
    }

    const hora = fecha.toLocaleTimeString("es-PE", {
        hour: "2-digit",
        minute: "2-digit"
    });

    return MENSAJES.SES02.replace("{hora}", hora);
}
