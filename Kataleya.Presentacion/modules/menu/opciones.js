const OPCIONES_POR_ROL = {
    Administrador: [
        ["Registrar pasaje", "Reservas, ventas y grupos."],
        ["Consultar pasajes", "Búsqueda y detalle de pasajes."],
        ["Cuadre de ventas", "Totales por emisor y periodo."],
        ["Gestión de usuarios", "Altas, bajas y desbloqueos."],
        ["Bitácora", "Historial de operaciones."],
        ["Reporte anual", "Generación y depuración."],
        ["Configuración", "Tarifas y parámetros del negocio."]
    ],
    "Encargado de ventas": [
        ["Registrar pasaje", "Reservas, ventas y grupos."],
        ["Consultar pasajes", "Búsqueda y detalle de pasajes."],
        ["Cuadre de ventas", "Su propio cuadre del día."]
    ]
};

export function opcionesParaRol(rol) {
    return OPCIONES_POR_ROL[rol] ?? [];
}
