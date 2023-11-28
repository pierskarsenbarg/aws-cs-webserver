using Pulumi;
using Pulumi.Aws.Ec2;
using Pulumi.Aws.Ec2.Inputs;
using Pulumi.Aws.S3;
using System.Collections.Generic;
using System.Linq;

return await Deployment.RunAsync(() =>
{
   var config = new Config();
   var stackConfig = new Pulumi.Config();
   var lz = new LandingZone(config.ProjectName, new()
   {
      CidrBlock = "10.0.0.0/20",
      PublicSubnetCidrBlocks = new Input<string>[] {"10.0.0.0/24", "10.0.1.0/24", "10.0.2.0/24"}
   });

   var webSg = new SecurityGroup($"{config.ProjectName}-sg", new()
   {
      VpcId = lz.Vpc.Id,
      Ingress = new[] 
      {
         new SecurityGroupIngressArgs 
         {
            Protocol = "tcp",
            FromPort = 22,
            ToPort = 22,
            CidrBlocks = lz.Vpc.CidrBlock
         },
         new SecurityGroupIngressArgs 
         {
            Protocol = "tcp",
            FromPort = 80,
            ToPort = 80,
            CidrBlocks = lz.Vpc.CidrBlock
         }
      },
      Egress = new[] 
      {
         new SecurityGroupEgressArgs 
         {
            Protocol = "-1",
            FromPort = 0,
            ToPort = 0,
            CidrBlocks = new[]{"0.0.0.0/0"}
         }
      }
   });

   var instanceCount = stackConfig.GetInt32("instanceCount") ?? 0;
   var instanceDnsNames = new List<Output<string>>();

   for(var i = 0; i < instanceCount; i++)
   {
      var webServerName = $"{config.ProjectName}-server-{i}";
      var webServer = new Instance(webServerName, new()
      {
         InstanceType = InstanceType.T3_Small,
         Ami = config.AmazonAmiId,
         SubnetId = lz.PublicSubnets[0].Id,
         VpcSecurityGroupIds = new InputList<string> {webSg.Id},
         AssociatePublicIpAddress = true,
         KeyName = config.SshKey.KeyName,
         Tags = new InputMap<string> {
            {"Name", "demo-instance"}
         }
      });

      instanceDnsNames.Add(webServer.PublicDns);
   }

   // Export the list of dns names (this doesn't work right now)
   return new Dictionary<string, object?>
   {
      ["instanceDnsNames"] = instanceDnsNames.AsEnumerable()
   };
});
