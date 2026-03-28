namespace TaskProcessor.Domain.Shared.Errors;

public static class JobErrors
{
    public static readonly DomainError PayloadRequired = DomainError.Validation(
        "Job.PayloadRequired",
        "O payload é obrigatório.");

    public static readonly DomainError InvalidMaxRetries = DomainError.Validation(
        "Job.InvalidMaxRetries",
        "O número máximo de tentativas deve ser maior que zero.");

    public static readonly DomainError TypeRequired = DomainError.Validation(
        "Job.TypeRequired",
        "O tipo da tarefa é obrigatório.");

    public static readonly DomainError NotFound = DomainError.NotFound(
        "Job.NotFound",
        "Tarefa não encontrada.");

    public static readonly DomainError InvalidStatusTransition = DomainError.Conflict(
        "Job.InvalidStatusTransition",
        "Transição de status inválida.");

    public static readonly DomainError MaxRetriesExceeded = DomainError.Conflict(
        "Job.MaxRetriesExceeded",
        "Número máximo de tentativas excedido.");

    public static readonly DomainError ErrorMessageRequired = DomainError.Validation(
        "Job.ErrorMessageRequired",
        "A mensagem de erro é obrigatória.");

    public static readonly DomainError LeaseExpired = DomainError.Conflict(
        "Job.LeaseExpired",
        "O lease expirou durante o processamento.");

    public static readonly DomainError IdRequired = DomainError.Validation(
        "Job.IdRequired",
        "O identificador do job é obrigatório.");
}
