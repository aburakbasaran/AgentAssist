using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace AgentAssist.Evaluation.Tests.DevCloud;

internal static class EvalChatClientRegistration
{
    public static void WrapChatClientWithTranscriptCapture(IServiceCollection services)
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(IChatClient)).ToList();
        if (descriptors.Count is 0)
        {
            return;
        }

        var innerDescriptor = descriptors[^1];
        foreach (var descriptor in descriptors)
        {
            services.Remove(descriptor);
        }

        services.Add(new ServiceDescriptor(
            typeof(IChatClient),
            sp =>
            {
                var inner = CreateChatClient(sp, innerDescriptor);
                var collector = sp.GetRequiredService<ChatTranscriptCollector>();
                return new TranscriptCapturingChatClient(inner, collector);
            },
            innerDescriptor.Lifetime));
    }

    private static IChatClient CreateChatClient(IServiceProvider sp, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is IChatClient instance)
        {
            return instance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return (IChatClient)descriptor.ImplementationFactory(sp);
        }

        if (descriptor.ImplementationType is not null)
        {
            return (IChatClient)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType);
        }

        throw new InvalidOperationException("Could not resolve inner IChatClient for transcript decorator.");
    }
}
