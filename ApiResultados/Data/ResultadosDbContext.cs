using ApiResultados.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiResultados.Data;

public sealed class ResultadosDbContext(DbContextOptions<ResultadosDbContext> options) : DbContext(options)
{
    public DbSet<Jogo> Jogos => Set<Jogo>();
    public DbSet<Resultado> Resultados => Set<Resultado>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Jogo>(entity =>
        {
            entity.ToTable("jogo");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CodigoJogo).HasColumnName("codigo_jogo").HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.DataHoraInicio).HasColumnName("data_hora_inicio");
            entity.Property(e => e.EquipaCasa).HasColumnName("equipa_casa").HasMaxLength(120).IsUnicode(false);
            entity.Property(e => e.EquipaFora).HasColumnName("equipa_fora").HasMaxLength(120).IsUnicode(false);
            entity.Property(e => e.TipoCompeticao).HasColumnName("tipo_competicao").HasMaxLength(80).IsUnicode(false);
            entity.Property(e => e.EstadoAtual).HasColumnName("estado_atual");
            entity.Property(e => e.GolosCasaAtual).HasColumnName("golos_casa_atual");
            entity.Property(e => e.GolosVisitanteAtual).HasColumnName("golos_visitante_atual");
            entity.Property(e => e.MarcadorAtualizado).HasColumnName("marcador_atualizado");
        });

        modelBuilder.Entity<Resultado>(entity =>
        {
            entity.ToTable("resultado", tableBuilder =>
            {
                tableBuilder.HasTrigger("trg_resultado_apenas_finalizado");
            });
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.JogoId).HasColumnName("jogo_id");
            entity.Property(e => e.GolosCasa).HasColumnName("golos_casa");
            entity.Property(e => e.GolosVisitante).HasColumnName("golos_visitante");
            entity.Property(e => e.ResultadoJogo)
                .HasColumnName("resultado")
                .HasMaxLength(1)
                .IsUnicode(false)
                .IsRequired();
            entity.Property(e => e.DataHoraAtualizacao).HasColumnName("data_hora_atualizacao");

            entity.HasOne(d => d.Jogo)
                .WithOne(p => p.Resultado)
                .HasForeignKey<Resultado>(d => d.JogoId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_resultado_jogo");
        });
    }
}
