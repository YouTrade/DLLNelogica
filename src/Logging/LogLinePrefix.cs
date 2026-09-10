namespace DLLNelogica.Logging;

internal enum LogLinePrefix
{
    // Data, hora e origem. O relato da sessão precisa ser legível fora de contexto.
    SessionStamp,

    // Apenas hora com milissegundos. Em arquivo de ticks, repetir data e origem a cada
    // linha é redundância pura — e a milhares de linhas por minuto, custa disco.
    TimeOfDay
}
