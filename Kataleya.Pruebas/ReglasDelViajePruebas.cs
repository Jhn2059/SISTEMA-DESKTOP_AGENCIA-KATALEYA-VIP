using Kataleya.Aplicacion;
using Kataleya.Dominio.Entities;
using Kataleya.Dominio.Enums;

namespace Kataleya.Pruebas;

public sealed class ReglasDelViajePruebas
{
    [Theory]
    [InlineData("Huancayo", SentidoDelViaje.Ida)]
    [InlineData("  huancayo ", SentidoDelViaje.Ida)]
    [InlineData("Ica", SentidoDelViaje.Vuelta)]
    [InlineData("ICA (agencia)", SentidoDelViaje.Vuelta)]
    public void El_sentido_lo_da_el_origen(string origen, SentidoDelViaje esperado)
    {
        Assert.Equal(esperado, Ruta.SentidoSegunElOrigen(origen));
    }

    [Theory]
    [InlineData("Chupaca")]
    [InlineData("Huancavelica")]
    [InlineData("")]
    [InlineData(null)]
    public void Un_origen_que_no_es_Huancayo_ni_Ica_no_tiene_sentido(string? origen)
    {
        Assert.Null(Ruta.SentidoSegunElOrigen(origen));
    }

    [Fact]
    public void La_salida_se_combina_en_hora_de_Peru()
    {
        var salida = FechasLocales.Combinar(new DateOnly(2026, 10, 29), new TimeOnly(10, 0));

        Assert.Equal(DateTimeKind.Utc, salida.Kind);
        Assert.Equal(new DateTime(2026, 10, 29, 15, 0, 0, DateTimeKind.Utc), salida);
    }

    [Fact]
    public void Hoy_es_la_fecha_de_Peru_y_no_la_de_UTC()
    {
        var ahora = new DateTime(2026, 10, 30, 2, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateOnly(2026, 10, 29), FechasLocales.Hoy(ahora));
    }
}
