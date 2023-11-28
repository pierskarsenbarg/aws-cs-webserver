using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Pulumi;
using Pulumi.Aws;
using Pulumi.Aws.Ec2;
using Pulumi.Aws.Budgets;
using Pulumi.Aws.CostExplorer;
using System;


public class LandingZone : ComponentResource
{
    public readonly Vpc Vpc;
    public readonly List<Subnet> PublicSubnets;
    public readonly List<Subnet> PrivateSubnets;

    public LandingZone(string name, LandingZoneArgs args, ComponentResourceOptions? opts = null) : base("custom:x:LandingZone", name, opts)
    {
        var account = GetCallerIdentity.Invoke();

        var budget = new Budget(name, new()
        {
            AccountId = account.Apply(account => account.AccountId),
            BudgetType = "COST",
            LimitAmount = args.MonthlyBudget ?? "500.0",
            LimitUnit = "USD",
            TimePeriodStart = "2010-01-01_00:00",
            TimeUnit = "MONTHLY"
        }, new()
        {
            Parent = this
        });

        var azs = GetAvailabilityZones.Invoke(new()
        {
            State = "available"
        });

        this.Vpc = new Vpc(name, new()
        {
            CidrBlock = args.CidrBlock,
            EnableDnsHostnames = true,
            Tags = HandleTags()
        }, new()
        {
            Parent = this
        });

        var internetGateway = new InternetGateway($"{name}-public", new()
        {
            VpcId = this.Vpc.Id,
            Tags = HandleTags()
        }, new()
        {
            Parent = this
        });

        var publicSubnetRouteTable = new RouteTable($"{name}-public", new()
        {
            VpcId = this.Vpc.Id,
            Tags = HandleTags()
        }, new()
        {
            Parent = this
        });
        var publicSubnetRoute = new Route($"{name}-public", new()
        {
            RouteTableId = publicSubnetRouteTable.Id,
            DestinationCidrBlock = "0.0.0.0/0",
            GatewayId = internetGateway.Id
        }, new()
        {
            Parent = this
        });

        this.PublicSubnets = new List<Subnet>();
        this.PrivateSubnets = new List<Subnet>();

        for (int i = 0; i < (args.PublicSubnetCidrBlocks?.Length -1 ?? 0); i++)
        {
            var azId = azs.Apply(az => az.ZoneIds[i]);

            var publicSubnet = new Subnet($"{name}-public={i}", new()
            {
                VpcId = this.Vpc.Id,
                AvailabilityZoneId = azId,
                CidrBlock = args.PublicSubnetCidrBlocks?[i],
                MapPublicIpOnLaunch = true,
                Tags = HandleTags()
            }, new()
            {
                Parent = this,
                DeleteBeforeReplace = true
            });

            PublicSubnets.Add(publicSubnet);

            var publicSubnetRouteTableAssociation = new RouteTableAssociation($"{name}-public-{i}", new()
            {
                SubnetId = publicSubnet.Id,
                RouteTableId = publicSubnetRouteTable.Id
            });

            if (args.PrivateSubnetCidrBlocks != null)
            {
                var natEip = new Eip($"{name}-public={i}", new()
                {
                    Vpc = true,
                    Tags = HandleTags()
                }, new()
                {
                    DependsOn = internetGateway,
                    Parent = this
                });

                var natGateway = new NatGateway($"{name}-public-{i}", new()
                {
                    SubnetId = publicSubnet.Id,
                    AllocationId = natEip.Id,
                    Tags = HandleTags()
                });

                var privateSubnet = new Subnet($"{name}-private-{i}", new()
                {
                    VpcId = this.Vpc.Id,
                    AvailabilityZoneId = azId,
                    CidrBlock = args.PrivateSubnetCidrBlocks[i],
                    MapPublicIpOnLaunch = false,
                    Tags = HandleTags()
                }, new()
                {
                    Parent = this.Vpc,
                    DeleteBeforeReplace = true
                });

                this.PrivateSubnets.Add(privateSubnet);

                var privateSubnetRouteTable = new RouteTable($"{name}-private-{i}", new()
                {
                    VpcId = this.Vpc.Id,
                    Tags = HandleTags()
                }, new()
                {
                    Parent = this.Vpc
                });

                var privateSubnetRoute = new Route($"{name}-private-{i}", new()
                {
                    RouteTableId = privateSubnetRouteTable.Id,
                    DestinationCidrBlock = "0.0.0.0/0",
                    NatGatewayId = natGateway.Id
                }, new()
                {
                    Parent = privateSubnetRouteTable
                });

                var privateSubnetRouteTableAssociation = new RouteTableAssociation($"{name}-private-{i}", new()
                {
                    SubnetId = privateSubnet.Id,
                    RouteTableId = privateSubnetRouteTable.Id
                }, new()
                {
                    Parent = privateSubnet
                });
            }
        }

        InputMap<string> HandleTags()
        {
            return args.Tags ?? new InputMap<string>();
        }
    }



}

public class LandingZoneArgs
{
    public Input<string> CidrBlock { get; set; }
    public Input<string>[] PublicSubnetCidrBlocks { get; set; }
    public Input<string>[] PrivateSubnetCidrBlocks { get; set; }
    public InputMap<string>? Tags { get; set; }
    public Input<string>? MonthlyBudget { get; set; }
}
