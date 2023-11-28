using Pulumi.Aws.Ec2;
using Pulumi.Aws.Ec2.Inputs;
using Pulumi;
using Pulumi.Tls;
using System.Linq;

public class Config
{
    public string ProjectName { get; set; }
    public string StackName { get; set; }
    public KeyPair SshKey { get; set; }
    public string TagAllResources { get; set; }
    public Output<string> AmazonAmiId { get; set; }

    public Output<string> UbuntuAmiId { get; set; }

    public Config()
    {
        var config = new Pulumi.Config();
        ProjectName = Pulumi.Deployment.Instance.ProjectName;
        StackName = Pulumi.Deployment.Instance.StackName;

        this.AmazonAmiId = GetAmi.Invoke(new()
        {
            Owners = new() { "137112412989" },
            MostRecent = true,
            Filters = new[]
            {
                new GetAmiFilterInputArgs
                {
                    Name = "name",
                    Values = new[] {"amzn2-ami-hvm-2.0.*-x86_64-gp2"}
                }
            }
        }).Apply(ami => ami.Id);

        this.UbuntuAmiId = GetAmi.Invoke(new()
        {
            Owners = new() { "099720109477" },
            MostRecent = true,
            Filters = new[]
            {
                new GetAmiFilterInputArgs
                {
                    Name = "name",
                    Values = new[] {"ubuntu/images/hvm-ssd/ubuntu-focal-20.04-amd64-server-*"}
                }
            }
        }).Apply(ami => ami.Id);

        var sshKeyMaterial = new PrivateKey(ProjectName, new()
        {
            Algorithm = "RSA"
        });

        this.SshKey = new KeyPair(ProjectName, new()
        {
            PublicKey = sshKeyMaterial.PublicKeyOpenssh
        });
    }
}