namespace TalepPazari.SharedKernel.Messaging;

// Stages an integration event in the module's current unit of work.
// Nothing is written until IUnitOfWork.SaveChangesAsync commits it together with the state change.
// Each module registers its own implementation as a keyed service (key: the module name).
public interface IOutbox
{
    void Add<TEvent>(TEvent integrationEvent)
        where TEvent : class;
}
