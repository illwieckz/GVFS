using RGFS.Common.FileSystem;
using RGFS.Common.NamedPipes;
using RGFS.Common.Tracing;

namespace RGFS.Service.Handlers
{
    public class AttachRgFltHandler : MessageHandler
    {
        private NamedPipeServer.Connection connection;
        private NamedPipeMessages.AttachRgFltRequest request;
        private ITracer tracer;

        public AttachRgFltHandler(
            ITracer tracer,
            NamedPipeServer.Connection connection,
            NamedPipeMessages.AttachRgFltRequest request)
        {
            this.tracer = tracer;
            this.connection = connection;
            this.request = request;
        }

        public void Run()
        {
            string errorMessage;
            NamedPipeMessages.CompletionState state = NamedPipeMessages.CompletionState.Success;
            if (!RgFltFilter.TryAttach(this.tracer, this.request.EnlistmentRoot, out errorMessage))
            {
                state = NamedPipeMessages.CompletionState.Failure;
                this.tracer.RelatedError("Unable to attach filter to volume. Enlistment root: {0} \nError: {1} ", this.request.EnlistmentRoot, errorMessage);
            }

            NamedPipeMessages.AttachRgFltRequest.Response response = new NamedPipeMessages.AttachRgFltRequest.Response();

            response.State = state;
            response.ErrorMessage = errorMessage;

            this.WriteToClient(response.ToMessage(), this.connection, this.tracer);
        }
    }
}
