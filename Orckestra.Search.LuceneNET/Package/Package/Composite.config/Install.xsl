<?xml version="1.0"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	<xsl:template match="@* | node()">
		<xsl:copy>
			<xsl:apply-templates select="@* | node()" />
		</xsl:copy>
	</xsl:template>

	<xsl:template match="/configuration/Composite.C1Console.Elements.Plugins.ElementProviderConfiguration/ElementProviderPlugins/add[@name='VirtualElementProvider']/Perspectives">
		<xsl:copy>
			<xsl:apply-templates select="@*" />

			<xsl:if test="count(add[@name='SearchPerspective'])=0">
				<add name="SearchPerspective" tag="Search" label="${{Composite.Search,SearchPerspective.Label}}" closeFolderIconName="Composite.Icons.generic-search" 
						 type="Composite.Plugins.Elements.ElementProviders.VirtualElementProvider.PlaceholderVirtualElement, Composite" 
						 path="${{root}}/console/index.html?pageId=search" IsTool="true">
					<Elements />
				</add>
			</xsl:if>

			<xsl:apply-templates select="node()" />
		</xsl:copy>
	</xsl:template>

</xsl:stylesheet>