using DevExpress.Xpo;
using Grpc.Core;
using System;
using System.Threading.Tasks;

namespace CalculateGrpc.Services
{
    public class CalculateService :  Calculator.CalculatorBase
    {
        public override Task<CalculatorReply> Calc(CalculatorRequest request, ServerCallContext context)
        {
            var exp = string.Empty;
            var result = 0d;
            switch (request.Op)
            {
                case "+":
                    exp = $"{request.Num1} + {request.Num2} =";
                    result = request.Num1 + request.Num2;
                    break;
                case "-":
                    exp = $"{request.Num1} - {request.Num2} =";
                    result = request.Num1 - request.Num2;
                    break;
                case "*":
                    exp = $"{request.Num1} * {request.Num2} =";
                    result = request.Num1 * request.Num2;
                    break;
                case "/":
                    exp = $"{request.Num1} / {request.Num2} = ";
                    result = request.Num1 / request.Num2;
                    break;
            }

            return Task.FromResult(new CalculatorReply() { Exp = exp, Result = result });
        }
    }

}